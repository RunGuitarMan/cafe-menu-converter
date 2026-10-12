using CafeMenuConverter.Core.Parsing;

namespace CafeMenuConverter.Core.Tests.Parsing;

public class TextJoinTests
{
    [Theory]
    [InlineData(new[] { "КАША РИСОВАЯ НА МОЛОКЕ", "БЕЗ МАСЛА" }, "КАША РИСОВАЯ НА МОЛОКЕ БЕЗ МАСЛА")]
    [InlineData(new[] { "РАССОЛЬНИК ПО-", "СТАРОРУССКИ ОВОЩНОЙ" }, "РАССОЛЬНИК ПО-СТАРОРУССКИ ОВОЩНОЙ")]
    [InlineData(new[] { "НАПИТОК ЯБЛОЧНО-", "КЛЮКВЕННЫЙ БЕЗ САХАРА" }, "НАПИТОК ЯБЛОЧНО-КЛЮКВЕННЫЙ БЕЗ САХАРА")]
    [InlineData(new[] { "ЯИЧНИЦА ГЛАЗУНЬЯ 1ЯЙЦО/", "ХЛЕБ БОРОДИНСКИЙ/ОВОЩИ/", "БЕКОН 125ГР Т БАНК" }, "ЯИЧНИЦА ГЛАЗУНЬЯ 1ЯЙЦО/ХЛЕБ БОРОДИНСКИЙ/ОВОЩИ/БЕКОН 125ГР Т БАНК")]
    [InlineData(new[] { "ПИЦЦА \"ПЕППЕРОНЕ\"" }, "ПИЦЦА \"ПЕППЕРОНЕ\"")]
    public void Joins_name_lines_keeping_real_hyphens(string[] lines, string expected)
    {
        Assert.Equal(expected, TextJoin.JoinName(lines));
    }

    [Theory]
    [InlineData(new[] { "ржа-", "ные" }, "ржаные")]
    [InlineData(new[] { "маслины/", "оливки" }, "маслины/оливки")]
    [InlineData(new[] { "сахар,", "соль" }, "сахар, соль")]
    public void Joins_composition_lines_removing_word_break_hyphen(string[] lines, string expected)
    {
        Assert.Equal(expected, TextJoin.JoinComposition(lines));
    }
}
