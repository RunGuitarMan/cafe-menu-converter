using CafeMenuConverter.Core.Parsing;

namespace CafeMenuConverter.Core.Tests.Parsing;

public class NutritionParserTests
{
    [Fact]
    public void Parses_line()
    {
        Assert.True(NutritionParser.TryParse("Б: 6 / Ж: 3 / У: 56 / Ккал 272", out var n));
        Assert.Equal(("6", "3", "56", "272"), (n.Protein, n.Fats, n.Carbohydrates, n.Calories));
        Assert.False(n.IsAllZero);
    }

    [Fact]
    public void Partial_zeros_are_kept()
    {
        Assert.True(NutritionParser.TryParse("Б: 0 / Ж: 0 / У: 26 / Ккал 107", out var n));
        Assert.Equal(("0", "0", "26", "107"), (n.Protein, n.Fats, n.Carbohydrates, n.Calories));
        Assert.False(n.IsAllZero);
    }

    [Fact]
    public void Detects_all_zero()
    {
        Assert.True(NutritionParser.TryParse("Б: 0 / Ж: 0 / У: 0 / Ккал 0", out var n));
        Assert.True(n.IsAllZero);
    }

    [Theory]
    [InlineData("Молоко 3,2%, рис")]
    [InlineData("Б: 6 / Ж: 3 / У: 56")]
    [InlineData("")]
    public void Rejects_other_text(string text)
    {
        Assert.False(NutritionParser.TryParse(text, out _));
    }

    [Theory]
    [InlineData("100 ₽", "100")]
    [InlineData("1150 ₽", "1150")]
    public void Parses_price(string text, string expected)
    {
        Assert.True(PriceParser.TryParse(text, out var price));
        Assert.Equal(expected, price);
    }

    [Theory]
    [InlineData("100")]
    [InlineData("₽")]
    [InlineData("сто ₽")]
    public void Rejects_bad_price(string text)
    {
        Assert.False(PriceParser.TryParse(text, out _));
    }
}
