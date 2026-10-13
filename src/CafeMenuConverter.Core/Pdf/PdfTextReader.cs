using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace CafeMenuConverter.Core.Pdf;

/// <summary>Слово страницы. Координаты в pt, Y отсчитывается сверху страницы (как при чтении).</summary>
public sealed record TextWord(string Text, double X0, double X1, double Baseline, double FontSize, bool IsWhite, string FontName = "")
{
    public double CenterX => (X0 + X1) / 2;

    public override string ToString() => $"«{Text}» (x={X0:F0}, y={Baseline:F0}, кегль {FontSize})";
}

public sealed record TextPage(int Number, double Width, double Height, IReadOnlyList<TextWord> Words);

/// <summary>
/// Извлекает слова из PDF через PdfPig. Слово — последовательность букв одной строки, одного кегля и цвета
/// без символа пробела между ними и без большого зазора (на табличных страницах числа стоят вплотную без пробелов).
/// </summary>
public static class PdfTextReader
{
    private const double MaxLetterGapEm = 0.5;
    private const double MinLetterGapEm = -0.5;
    private const double BaselineTolerance = 0.5;

    public static IReadOnlyList<TextPage> Read(string path, CancellationToken cancellationToken = default)
    {
        using var stream = File.OpenRead(path);
        return Read(stream, cancellationToken);
    }

    public static IReadOnlyList<TextPage> Read(Stream stream, CancellationToken cancellationToken = default)
    {
        using var document = PdfDocument.Open(stream);
        var pages = new List<TextPage>(document.NumberOfPages);
        foreach (var page in document.GetPages())
        {
            cancellationToken.ThrowIfCancellationRequested();
            pages.Add(new TextPage(page.Number, page.Width, page.Height, ExtractWords(page)));
        }
        return pages;
    }

    private static List<TextWord> ExtractWords(Page page)
    {
        var words = new List<TextWord>();
        var current = new List<Letter>();

        foreach (var letter in page.Letters)
        {
            if (string.IsNullOrWhiteSpace(letter.Value))
            {
                Flush();
                continue;
            }

            if (current.Count > 0 && !Continues(current[^1], letter))
                Flush();
            current.Add(letter);
        }

        Flush();
        return words;

        void Flush()
        {
            if (current.Count == 0)
                return;
            words.Add(ToWord(current, page.Height));
            current.Clear();
        }
    }

    private static bool Continues(Letter previous, Letter next)
    {
        if (Math.Abs(previous.StartBaseLine.Y - next.StartBaseLine.Y) > BaselineTolerance)
            return false;
        if (Math.Abs(Size(previous) - Size(next)) > 0.01)
            return false;
        if (IsWhite(previous) != IsWhite(next))
            return false;
        if (previous.FontName != next.FontName)
            return false;

        var gap = next.StartBaseLine.X - previous.EndBaseLine.X;
        var em = Math.Max(1.0, next.PointSize);
        return gap <= MaxLetterGapEm * em && gap >= MinLetterGapEm * em;
    }

    private static TextWord ToWord(List<Letter> letters, double pageHeight)
    {
        var first = letters[0];
        return new TextWord(
            Text: string.Concat(letters.Select(l => l.Value)),
            X0: letters.Min(l => l.BoundingBox.Left),
            X1: letters.Max(l => l.BoundingBox.Right),
            Baseline: pageHeight - first.StartBaseLine.Y,
            FontSize: Size(first),
            IsWhite: IsWhite(first),
            FontName: first.FontName ?? "");
    }

    private static double Size(Letter letter) => Math.Round(letter.PointSize, 1);

    private static bool IsWhite(Letter letter)
    {
        if (letter.Color is null)
            return false;
        var (r, g, b) = letter.Color.ToRGBValues();
        return r > 0.95 && g > 0.95 && b > 0.95;
    }
}
