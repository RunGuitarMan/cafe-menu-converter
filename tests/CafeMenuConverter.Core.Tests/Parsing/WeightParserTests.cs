using CafeMenuConverter.Core.Parsing;

namespace CafeMenuConverter.Core.Tests.Parsing;

public class WeightParserTests
{
    [Theory]
    // масса одной частью
    [InlineData("250 мл.", new[] { "250" }, "мл.", "")]
    [InlineData("250мл", new[] { "250" }, "мл.", "")]
    [InlineData("250 мл", new[] { "250" }, "мл.", "")]
    [InlineData("150 гр.", new[] { "150" }, "гр.", "")]
    [InlineData("150гр", new[] { "150" }, "гр.", "")]
    [InlineData("100 гр", new[] { "100" }, "гр.", "")]
    [InlineData("90 гр", new[] { "90" }, "гр.", "")]
    // несколько частей
    [InlineData("150/20 гр.", new[] { "150", "20" }, "гр.", "")]
    [InlineData("100/40гр", new[] { "100", "40" }, "гр.", "")]
    [InlineData("140/100/40 гр.", new[] { "140", "100", "40" }, "гр.", "")]
    [InlineData("250/100гр", new[] { "250", "100" }, "гр.", "")]
    // без единицы — граммы
    [InlineData("120/20", new[] { "120", "20" }, "гр.", "")]
    // штуки + граммы
    [InlineData("1 шт (65гр).", new[] { "65" }, "гр.", "1")]
    [InlineData("1кус (90гр).", new[] { "90" }, "гр.", "1")]
    [InlineData("1шт (90гр)", new[] { "90" }, "гр.", "1")]
    [InlineData("40гр (1шт)", new[] { "40" }, "гр.", "1")]
    [InlineData("40гр. (1шт)", new[] { "40" }, "гр.", "1")]
    // только штуки
    [InlineData("3 шт.", new string[0], "", "3")]
    [InlineData("2 шт.", new string[0], "", "2")]
    [InlineData("1 шт", new string[0], "", "1")]
    [InlineData("1 шт.", new string[0], "", "1")]
    public void Parses_known_formats(string text, string[] weights, string unit, string quantity)
    {
        var ok = WeightParser.TryParse(text, out var result);

        Assert.True(ok, text);
        Assert.Equal(weights, result.Weights);
        Assert.Equal(unit, result.Unit);
        Assert.Equal(quantity, result.Quantity);
    }

    [Fact]
    public void Empty_text_means_no_weight()
    {
        Assert.True(WeightParser.TryParse("", out var result));
        Assert.Empty(result.Weights);
        Assert.Equal("", result.Unit);
        Assert.Equal("", result.Quantity);
    }

    [Theory]
    [InlineData("гр.")]
    [InlineData("сто грамм")]
    [InlineData("150 кг")]
    [InlineData("1,5 л")]
    [InlineData("150//20 гр.")]
    [InlineData("1 шт (65мл)")]
    public void Rejects_unknown_formats(string text)
    {
        Assert.False(WeightParser.TryParse(text, out _));
    }
}
