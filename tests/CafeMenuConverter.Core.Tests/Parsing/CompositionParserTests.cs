using CafeMenuConverter.Core.Parsing;

namespace CafeMenuConverter.Core.Tests.Parsing;

public class CompositionParserTests
{
    [Fact]
    public void Splits_by_comma_and_keeps_decimal_percentages()
    {
        var result = CompositionParser.Parse(new[] { "Молоко 3,2%, рис, сахар, соль" });

        Assert.Equal(new[] { "Молоко 3,2%", "рис", "сахар", "соль" }, result.Items);
        Assert.True(result.IsBalanced);
    }

    [Fact]
    public void Keeps_decimal_without_percent()
    {
        var result = CompositionParser.Parse(new[] { "Мука, сахар, сироп ванильный, молоко 3,25, яйцо куриное,", "масло сливочное 82,5%, черника, разрыхлитель" });

        Assert.Equal(new[] { "Мука", "сахар", "сироп ванильный", "молоко 3,25", "яйцо куриное", "масло сливочное 82,5%", "черника", "разрыхлитель" }, result.Items);
    }

    [Fact]
    public void Does_not_split_inside_parentheses_and_joins_hyphenated_word_break()
    {
        var result = CompositionParser.Parse(new[]
        {
            "Сырники классические (данный продукт является полуфа-",
            "брикатом), сметана 20%, малина, сахар",
        });

        Assert.Equal(new[] { "Сырники классические (данный продукт является полуфабрикатом)", "сметана 20%", "малина", "сахар" }, result.Items);
    }

    [Fact]
    public void Semi_finished_note_is_one_item()
    {
        var result = CompositionParser.Parse(new[] { "Данный продукт является полуфабрикатом (говядина,", "свинина)" });

        Assert.Equal(new[] { "Данный продукт является полуфабрикатом (говядина, свинина)" }, result.Items);
    }

    [Fact]
    public void Line_break_hyphen_inside_real_hyphenated_word()
    {
        var result = CompositionParser.Parse(new[] { "булочка пшеничная, сосиска венская, соус медово-горчич-", "ный, соус кетчуп" });

        Assert.Equal(new[] { "булочка пшеничная", "сосиска венская", "соус медово-горчичный", "соус кетчуп" }, result.Items);
    }

    [Fact]
    public void Slash_at_line_end_joins_without_space()
    {
        var result = CompositionParser.Parse(new[] { "масло растительное, семена подсолнечника, маслины/", "оливки" });

        Assert.Equal(new[] { "масло растительное", "семена подсолнечника", "маслины/оливки" }, result.Items);
    }

    [Fact]
    public void Keeps_typos_and_dots_as_is()
    {
        var result = CompositionParser.Parse(new[] { "Йогурт 5%. клубника, апельсин, сироп Гренадин, сахар" });

        Assert.Equal(new[] { "Йогурт 5%. клубника", "апельсин", "сироп Гренадин", "сахар" }, result.Items);
    }

    [Fact]
    public void Nested_parentheses_across_lines()
    {
        var result = CompositionParser.Parse(new[]
        {
            "Курица, рис, лук репчатый, мука, соль, специи, масло расти-",
            "тельное, соус (лук репчатый, морковь, масло растительное,",
            "специи, мука, томатная паста, сахар, соль, масло сливочное",
            "82,5%)",
        });

        Assert.Equal(new[]
        {
            "Курица", "рис", "лук репчатый", "мука", "соль", "специи", "масло растительное",
            "соус (лук репчатый, морковь, масло растительное, специи, мука, томатная паста, сахар, соль, масло сливочное 82,5%)",
        }, result.Items);
        Assert.True(result.IsBalanced);
    }

    [Fact]
    public void Empty_composition()
    {
        var result = CompositionParser.Parse(Array.Empty<string>());

        Assert.Empty(result.Items);
    }

    [Fact]
    public void Reports_unbalanced_parentheses()
    {
        var result = CompositionParser.Parse(new[] { "соус (лук, морковь" });

        Assert.False(result.IsBalanced);
    }

    [Fact]
    public void Serializes_to_compact_json_without_escaping_cyrillic()
    {
        Assert.Equal("[\"Мука\",\"Сливочное масло\",\"Молоко 3,2%\"]", JsonList.Serialize(new[] { "Мука", "Сливочное масло", "Молоко 3,2%" }));
        Assert.Equal("[\"150\",\"20\"]", JsonList.Serialize(new[] { "150", "20" }));
        Assert.Equal("", JsonList.Serialize(Array.Empty<string>()));
    }
}
