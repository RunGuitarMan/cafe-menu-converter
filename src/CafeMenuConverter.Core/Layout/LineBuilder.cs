using CafeMenuConverter.Core.Pdf;

namespace CafeMenuConverter.Core.Layout;

public sealed record TextLine(IReadOnlyList<TextWord> Words)
{
    public double Y => Words.Average(w => w.Baseline);
    public string Text => string.Join(" ", Words.OrderBy(w => w.X0).Select(w => w.Text));
}

public static class LineBuilder
{
    public static IReadOnlyList<TextLine> Build(IEnumerable<TextWord> words, double tolerance = 1)
    {
        var lines = new List<List<TextWord>>();
        foreach (var word in words.OrderBy(w => w.Baseline).ThenBy(w => w.X0))
        {
            if (lines.Count == 0 || Math.Abs(word.Baseline - lines[^1][0].Baseline) > tolerance)
                lines.Add([]);
            lines[^1].Add(word);
        }
        return lines.Select(l => new TextLine(l.OrderBy(w => w.X0).ToArray())).ToArray();
    }
}
