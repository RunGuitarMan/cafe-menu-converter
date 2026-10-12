using System.Text.RegularExpressions;

namespace CafeMenuConverter.Core.Parsing;

public sealed record Nutrition(string Protein, string Fats, string Carbohydrates, string Calories)
{
    public bool IsAllZero => Protein == "0" && Fats == "0" && Carbohydrates == "0" && Calories == "0";
}

public static partial class NutritionParser
{
    [GeneratedRegex(@"^Б:\s*(?<p>\d+)\s*/\s*Ж:\s*(?<f>\d+)\s*/\s*У:\s*(?<c>\d+)\s*/\s*Ккал\s*(?<k>\d+)$")]
    private static partial Regex LineRegex();

    [GeneratedRegex(@"^Б:\s*\d")]
    private static partial Regex StartRegex();

    public static bool TryParse(string text, out Nutrition nutrition)
    {
        var m = LineRegex().Match(text.Trim());
        nutrition = m.Success
            ? new Nutrition(m.Groups["p"].Value, m.Groups["f"].Value, m.Groups["c"].Value, m.Groups["k"].Value)
            : new Nutrition("", "", "", "");
        return m.Success;
    }

    /// <summary>Строка выглядит как строка КБЖУ (даже если разобрать её не удалось).</summary>
    public static bool LooksLikeNutrition(string text) => StartRegex().IsMatch(text.Trim());
}

public static partial class PriceParser
{
    [GeneratedRegex(@"^(?<v>\d+)\s*₽$")]
    private static partial Regex PriceRegex();

    public static bool TryParse(string text, out string price)
    {
        var m = PriceRegex().Match(text.Trim());
        price = m.Success ? m.Groups["v"].Value : "";
        return m.Success;
    }
}
