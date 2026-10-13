using System.Globalization;
using System.Text.RegularExpressions;
using CafeMenuConverter.Core.Pdf;

namespace CafeMenuConverter.Core.Layout;

public sealed record PageHeader(DateOnly Date, string MealType, double BodyTop, double BodyBottom);

public static partial class PageHeaderParser
{
    [GeneratedRegex(@"\b(?<d>\d{2})\.(?<m>\d{2})\.(?<y>\d{2}|\d{4})\b")]
    private static partial Regex DateRegex();
    [GeneratedRegex(@"\b\d{2}:\d{2}\s*[-–—]\s*\d{2}:\d{2}\b")]
    private static partial Regex TimeRegex();

    public static PageHeader Parse(TextPage page)
    {
        var top = page.Words.Where(w => w.Baseline < page.Height * .13).ToArray();
        var text = string.Join(" ", LineBuilder.Build(top).Select(l => l.Text));
        var dates = DateRegex().Matches(text);
        var times = TimeRegex().Matches(text);
        if (dates.Count != 1 || times.Count != 1)
            throw new FormatException("Не найдена однозначная шапка с датой и временем. Нужен текстовый PDF установленного шаблона.");
        var match = dates[0];
        var year = int.Parse(match.Groups["y"].Value, CultureInfo.InvariantCulture);
        if (year < 100) year += 2000;
        DateOnly date;
        try { date = new DateOnly(year, int.Parse(match.Groups["m"].Value), int.Parse(match.Groups["d"].Value)); }
        catch (ArgumentOutOfRangeException) { throw new FormatException($"Некорректная дата: {match.Value}"); }
        var time = Regex.Replace(times[0].Value, @"\s|[–—]", m => m.Value is "–" or "—" ? "-" : "");
        var meal = time switch
        {
            "08:30-10:30" => "Завтрак",
            "12:00-17:00" => "Обед",
            _ => throw new FormatException($"Неизвестное время меню: {times[0].Value}")
        };
        var headerBottom = top.Where(w => DateRegex().IsMatch(w.Text) || w.Text.Contains(':')).Max(w => w.Baseline);
        var footer = page.Words.Where(w => w.Baseline > page.Height * .9 &&
            (w.Text == "Обратите" || w.Text == "ВСЯ")).ToArray();
        if (footer.Length == 0) throw new FormatException("Не найдена нижняя граница меню (подвал).");
        return new PageHeader(date, meal, headerBottom + 2, footer.Min(w => w.Baseline) - 4);
    }
}
