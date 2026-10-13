using System.ComponentModel;
using System.Runtime.CompilerServices;
using CafeMenuConverter.Core;
using CafeMenuConverter.Core.Export;
using CafeMenuConverter.Core.Model;

namespace CafeMenuConverter.App;

public sealed class MainViewModel(string? configurationPath = null) : INotifyPropertyChanged
{
    private string inputPath = "", outputPath = "", status = "Выберите PDF и офис, затем нажмите «Конвертировать».";
    private Office? office;
    private bool busy;
    private CancellationTokenSource? cancellation;
    public event PropertyChangedEventHandler? PropertyChanged;
    public IReadOnlyList<Office> Offices => Office.All;
    public string InputPath
    {
        get => inputPath;
        set
        {
            if (inputPath == value) return;
            inputPath = value;
            OutputPath = string.IsNullOrWhiteSpace(value) ? "" : Path.ChangeExtension(value, ".xlsx");
            ResetResult(); Status = "Выберите офис и путь сохранения, затем нажмите «Конвертировать»."; Changed(); Changed(nameof(CanConvert));
        }
    }
    public string OutputPath { get => outputPath; set { outputPath = value; SavedPath = ""; Changed(); Changed(nameof(CanOpenFolder)); Changed(nameof(CanConvert)); } }
    public Office? SelectedOffice { get => office; set { office = value; ResetResult(); Status = "Нажмите «Конвертировать», чтобы сохранить меню для выбранного офиса."; Changed(); Changed(nameof(CanConvert)); } }
    public bool IsBusy { get => busy; private set { busy = value; Changed(); Changed(nameof(IsIdle)); Changed(nameof(CanConvert)); Changed(nameof(CanOpenFolder)); } }
    public bool IsIdle => !IsBusy;
    public bool CanConvert => IsIdle && office is not null && File.Exists(InputPath) &&
        string.Equals(Path.GetExtension(InputPath), ".pdf", StringComparison.OrdinalIgnoreCase) &&
        string.Equals(Path.GetExtension(OutputPath), ".xlsx", StringComparison.OrdinalIgnoreCase);
    public bool CanOpenFolder => IsIdle && SavedPath.Length > 0 && File.Exists(SavedPath);
    public string Status { get => status; private set { status = value; Changed(); } }
    public string Summary { get; private set; } = "Здесь появятся даты, количество блюд и разделы меню.";
    public string Diagnostics { get; private set; } = "";
    public IReadOnlyList<MenuItem> Items { get; private set; } = [];
    public string SavedPath { get; private set; } = "";
    public bool HasItems => Items.Count > 0;
    public bool HasDiagnostics => Diagnostics.Length > 0;
    public string DiagnosticsPlaceholder => HasItems ? "Проверка завершена. Замечаний нет." : "После конвертации здесь появятся результаты проверки меню.";
    public IReadOnlyList<SummaryRow> DaySummaries => Items.GroupBy(i => (i.Date, i.MealType))
        .OrderBy(g => g.Key.Date).Select(g => new SummaryRow(g.Key.Date.ToString("dd.MM.yyyy"), g.Key.MealType.ToString(), g.Count())).ToArray();
    public IReadOnlyList<SummaryRow> CategorySummaries => Items.GroupBy(i => i.Category)
        .Select(g => new SummaryRow(g.Key, "", g.Count())).ToArray();

    public sealed record SummaryRow(string Label, string Detail, int Count);

    public async Task ConvertAsync(Func<string, Task<bool>> confirmOverwrite)
    {
        if (!CanConvert) return;
        // Snapshot inputs before the first await. Controls stay disabled until the write has completed.
        var inputText = InputPath;
        var outputText = OutputPath;
        var selected = SelectedOffice!;
        IsBusy = true;
        ResetResult();
        cancellation = new CancellationTokenSource();
        try
        {
            var input = Path.GetFullPath(inputText);
            var output = Path.GetFullPath(outputText);
            Status = "Читаем PDF и проверяем блюда…";
            var token = cancellation.Token;
            var result = await Task.Run(() =>
            {
                var config = AppConfiguration.Load(configurationPath);
                return new MenuConverter(config).Convert(input, token);
            }, token);
            Items = result.Items;
            Diagnostics = string.Join(Environment.NewLine + Environment.NewLine, result.Diagnostics);
            Summary = string.Join(Environment.NewLine, result.Items.GroupBy(i => (i.Date, i.MealType))
                .OrderBy(g => g.Key.Date).Select(g => $"{g.Key.Date:dd.MM.yyyy} · {g.Key.MealType} · блюд: {g.Count()}")) +
                Environment.NewLine + Environment.NewLine + string.Join(Environment.NewLine,
                    result.Items.GroupBy(i => i.Category).Select(g => $"{g.Key}: {g.Count()}"));
            NotifyResultChanged();
            if (!result.Success) { Status = "Обнаружены ошибки. XLSX не сохранён. Подробности — во вкладке «Диагностика»."; return; }
            token.ThrowIfCancellationRequested();
            var overwrite = File.Exists(output);
            if (overwrite && !await confirmOverwrite(output)) { Status = "Сохранение отменено. Существующий файл сохранён."; return; }
            Status = "Сохраняем XLSX…";
            await Task.Run(() => XlsxMenuWriter.Write(output, result, selected, overwrite, token), token);
            SavedPath = output;
            Status = $"Готово · блюд: {result.Items.Count} · предупреждений: {result.Diagnostics.Count} · {selected.Name}";
        }
        catch (OperationCanceledException) { Status = "Конвертация отменена. Файл результата не изменён."; }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Status = "Не удалось выполнить конвертацию.";
            Diagnostics = ex is IOException or UnauthorizedAccessException
                ? $"{ex.Message}\nПроверьте доступ к папке. Если XLSX открыт в Excel, закройте его и повторите попытку."
                : ex.Message;
            Changed(nameof(Diagnostics));
            Changed(nameof(HasDiagnostics));
        }
        finally { cancellation.Dispose(); cancellation = null; IsBusy = false; }
    }

    public void Cancel() => cancellation?.Cancel();
    public void ReportError(string message) { Status = message; }
    private void ResetResult()
    {
        SavedPath = ""; Items = []; Diagnostics = "";
        Summary = "Здесь появятся даты, количество блюд и разделы меню.";
        NotifyResultChanged(); Changed(nameof(CanOpenFolder));
    }
    private void NotifyResultChanged()
    {
        Changed(nameof(Items)); Changed(nameof(Diagnostics)); Changed(nameof(Summary));
        Changed(nameof(HasItems)); Changed(nameof(HasDiagnostics)); Changed(nameof(DiagnosticsPlaceholder));
        Changed(nameof(DaySummaries)); Changed(nameof(CategorySummaries));
    }
    private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
