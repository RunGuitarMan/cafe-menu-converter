using System.Text.RegularExpressions;

namespace CafeMenuConverter.Core.Parsing;

public sealed record WeightInfo(IReadOnlyList<string> Weights, string Unit, string Quantity)
{
    public static readonly WeightInfo None = new(Array.Empty<string>(), "", "");
}

/// <summary>
/// Разбор выхода блюда строго по известной грамматике. Незнакомый формат — не угадываем, возвращаем false.
/// </summary>
public static partial class WeightParser
{
    // «250 мл.», «150гр», «150/20 гр.», «120/20» (без единицы — граммы)
    [GeneratedRegex(@"^(?<w>\d+(?:/\d+)*)\s*(?:(?<u>гр|мл)\.?)?$")]
    private static partial Regex MassRegex();

    // «1 шт (65гр).», «1кус (90гр).», «1шт (90гр)»
    [GeneratedRegex(@"^(?<q>\d+)\s*(?:шт|кус)\.?\s*\(\s*(?<w>\d+)\s*гр\.?\s*\)\.?$")]
    private static partial Regex PiecesThenGramsRegex();

    // «40гр (1шт)», «40гр. (1шт)»
    [GeneratedRegex(@"^(?<w>\d+)\s*гр\.?\s*\(\s*(?<q>\d+)\s*(?:шт|кус)\.?\s*\)\.?$")]
    private static partial Regex GramsThenPiecesRegex();

    // «3 шт.», «1 шт»
    [GeneratedRegex(@"^(?<q>\d+)\s*(?:шт|кус)\.?$")]
    private static partial Regex PiecesRegex();

    public static bool TryParse(string text, out WeightInfo result)
    {
        var s = Regex.Replace(text.Trim(), @"\s+", " ");
        result = WeightInfo.None;
        if (s.Length == 0)
            return true;

        var m = MassRegex().Match(s);
        if (m.Success)
        {
            var unit = m.Groups["u"].Success && m.Groups["u"].Value == "мл" ? "мл." : "гр.";
            result = new WeightInfo(m.Groups["w"].Value.Split('/'), unit, "");
            return true;
        }

        m = PiecesThenGramsRegex().Match(s);
        if (!m.Success)
            m = GramsThenPiecesRegex().Match(s);
        if (m.Success)
        {
            result = new WeightInfo(new[] { m.Groups["w"].Value }, "гр.", m.Groups["q"].Value);
            return true;
        }

        m = PiecesRegex().Match(s);
        if (m.Success)
        {
            result = new WeightInfo(Array.Empty<string>(), "", m.Groups["q"].Value);
            return true;
        }

        return false;
    }
}
