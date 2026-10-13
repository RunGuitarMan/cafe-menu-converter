using CafeMenuConverter.Core.Diagnostics;
using CafeMenuConverter.Core.Layout;
using CafeMenuConverter.Core.Model;
using CafeMenuConverter.Core.Parsing;
using CafeMenuConverter.Core.Pdf;
using CafeMenuConverter.Core.Configuration;

namespace CafeMenuConverter.Core;

public sealed record ConversionResult(IReadOnlyList<MenuItem> Items, IReadOnlyList<Diagnostic> Diagnostics)
{
    public bool Success => Items.Count > 0 && Diagnostics.All(d => d.Severity != Severity.Error);
}

public sealed class MenuConverter(MenuConfiguration? configuration = null)
{
    public ConversionResult Convert(string pdfPath, CancellationToken cancellationToken = default)
    {
        try { return ConvertPages(PdfTextReader.Read(pdfPath, cancellationToken), cancellationToken); }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return new ConversionResult([], [new Diagnostic(Severity.Error, $"Не удалось прочитать PDF: {ex.Message}")]);
        }
    }

    public ConversionResult ConvertPages(IReadOnlyList<TextPage> pages, CancellationToken cancellationToken = default)
    {
        var diagnostics = new DiagnosticBag();
        var assembler = new DishAssembler(diagnostics);
        var headers = new List<(TextPage Page, PageHeader Header)>();
        foreach (var page in pages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try { headers.Add((page, PageHeaderParser.Parse(page))); }
            catch (FormatException ex) { diagnostics.Error(ex.Message, page.Number); }
        }
        if (headers.Select(p => p.Header.MealType).Distinct().Count() > 1)
            diagnostics.Error("В одном PDF обнаружены разные типы питания.");
        foreach (var day in headers.GroupBy(p => p.Header.Date))
        {
            var before = assembler.Items.Count;
            foreach (var (page, header) in day)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try { assembler.Read(page, header); }
                catch (FormatException ex) { diagnostics.Error(ex.Message, page.Number); }
            }
            assembler.EndDay();
            var prices = day.Sum(p => p.Page.Words.Sum(w => w.Text.Count(c => c == '₽')));
            var count = assembler.Items.Count - before;
            if (count != prices) diagnostics.Error($"{day.Key:dd.MM.yyyy}: распознано {count} блюд, найдено {prices} цен.");
        }
        foreach (var duplicate in assembler.Items.GroupBy(i => (i.Date, i.Name, i.Price)).Where(g => g.Count() > 1))
            diagnostics.Warning($"Повтор блюда «{duplicate.Key.Name}» за {duplicate.Key.Date:dd.MM.yyyy}.");
        if (configuration is not null)
            foreach (var category in assembler.Items.GroupBy(i => i.Category).Where(g => !configuration.Contains(g.Key)))
                diagnostics.Error($"Неизвестный раздел «{category.Key}». Добавьте его в categories.json; название будет перенесено дословно.", category.First().Page);
        if (assembler.Items.Count == 0) diagnostics.Error("Блюда не найдены.");
        return new ConversionResult(assembler.Items.ToArray(), diagnostics.Items.ToArray());
    }
}
