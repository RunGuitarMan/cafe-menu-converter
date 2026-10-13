using CafeMenuConverter.Core.Configuration;
using CafeMenuConverter.Core.Diagnostics;
using CafeMenuConverter.Core.Layout;
using CafeMenuConverter.Core.Pdf;
using CafeMenuConverter.Core.Model;
using CafeMenuConverter.Core.Export;
using System.Text.Json;

namespace CafeMenuConverter.Core.Tests;

public class ConversionTests
{
    internal static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);
    internal static ConversionResult Read(string name) => new MenuConverter(MenuConfiguration.Load(Path.Combine(AppContext.BaseDirectory, "categories.json"))).Convert(Fixture(name));
    private static readonly Lazy<ConversionResult> Breakfast = new(() => Read("breakfast.pdf"));
    private static readonly Lazy<ConversionResult> Lunch = new(() => Read("lunch.pdf"));

    [Theory]
    [InlineData("breakfast")]
    [InlineData("lunch")]
    public void Every_field_matches_independently_extracted_fixture(string name)
    {
        var expected = JsonSerializer.Deserialize<MenuItem[]>(File.ReadAllText(Fixture($"expected-{name}.json")))!;
        var actual = name == "breakfast" ? Breakfast.Value : Lunch.Value;
        Assert.Equal(expected.Length, actual.Items.Count);
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.Equal(XlsxMenuWriter.Values(expected[i], Office.Space), XlsxMenuWriter.Values(actual.Items[i], Office.Space));
            Assert.Equal(expected[i].Page, actual.Items[i].Page);
        }
    }

    [Theory]
    [InlineData("breakfast.pdf", 145, 2, "Завтрак", new[] { 30, 28, 30, 28, 29 })]
    [InlineData("lunch.pdf", 155, 0, "Обед", new[] { 31, 31, 31, 31, 31 })]
    public void Reads_complete_menu(string file, int count, int warnings, string meal, int[] daily)
    {
        var result = file == "breakfast.pdf" ? Breakfast.Value : Lunch.Value;
        Assert.True(result.Success, string.Join("\n", result.Diagnostics));
        Assert.Equal(count, result.Items.Count);
        Assert.Equal(warnings, result.Diagnostics.Count);
        Assert.All(result.Diagnostics, d => Assert.Equal(Severity.Warning, d.Severity));
        Assert.All(result.Items, i => { Assert.Equal(meal, i.MealType); Assert.NotEmpty(i.Name); Assert.NotEmpty(i.Category); Assert.NotEmpty(i.Price); });
        Assert.Equal(daily, result.Items.GroupBy(i => i.Date).OrderBy(g => g.Key).Select(g => g.Count()));
        Assert.Equal(new DateOnly(2026, 10, 12), result.Items.Min(i => i.Date));
        Assert.Equal(new DateOnly(2026, 10, 16), result.Items.Max(i => i.Date));
    }

    [Fact]
    public void Preserves_name_and_nutrition_across_columns()
    {
        var sausage = Assert.Single(Lunch.Value.Items, i => i.Name == "КОЛБАСКА МЮНХЕНСКАЯ С МЕДОВО-ГОРЧИЧНЫМ СОУСОМ");
        Assert.Equal(("210", "9", "32", "6", "347"), (sausage.Price, sausage.Protein, sausage.Fats, sausage.Carbohydrates, sausage.Calories));
        Assert.Equal(new[] { "80", "20" }, sausage.Weights);
        Assert.Equal("Горячее", sausage.Category);
        var omelet = Assert.Single(Breakfast.Value.Items, i => i.Name == "ОМЛЕТ БЕЛКОВЫЙ С ПАСТРАМИ ИЗ ИНДЕЙКИ");
        Assert.Equal(("19", "7", "3", "146"), (omelet.Protein, omelet.Fats, omelet.Carbohydrates, omelet.Calories));
        Assert.Contains("масло растительное", omelet.Composition);
        Assert.DoesNotContain(omelet.Composition, s => s.Contains("раститель-"));
    }

    [Fact]
    public void Preserves_source_anomalies_and_empty_values_without_special_cases()
    {
        var item = Assert.Single(Breakfast.Value.Items, i => i.Name.EndsWith("125ГР Т БАНК"));
        Assert.Equal("ЯИЧНИЦА ГЛАЗУНЬЯ 1ЯЙЦО/ХЛЕБ БОРОДИНСКИЙ/ОВОЩИ/БЕКОН 125ГР Т БАНК", item.Name);
        Assert.Empty(item.Weights); Assert.Empty(item.Quantity); Assert.Empty(item.Unit);
        Assert.Equal(("200", "", "", "", ""), (item.Price, item.Protein, item.Fats, item.Carbohydrates, item.Calories));
        Assert.All(Breakfast.Value.Items.Where(i => i.Name == "ТВОРОГ"), i => Assert.Empty(i.Composition));
        var dumplings = Assert.Single(Breakfast.Value.Items, i => i.Name == "ВАРЕНИКИ СО СМЕТАНОЙ");
        Assert.Equal(new[] { "150", "20" }, dumplings.Weights);
        Assert.Empty(dumplings.Calories);
        Assert.Contains(Breakfast.Value.Items, i => i.Name == "ТВОРОГ С МАЛИНОЙ" && i.Composition.Contains("клубника"));
    }

    [Fact]
    public void Reads_table_columns_and_split_weight()
    {
        var item = Assert.Single(Breakfast.Value.Items, i => i.Name == "КУСКУС С КОПЧЕНОЙ КУРИЦЕЙ И СКРЕМБЛОМ");
        Assert.Equal(("350", "29", "20", "53", "504", "БЛЮДО ОТ ШЕФА"), (item.Price, item.Protein, item.Fats, item.Carbohydrates, item.Calories, item.Category));
        Assert.Equal(new[] { "255" }, item.Weights);
        Assert.All(Breakfast.Value.Items.Where(i => i.Name == "СОСИСКА СЛИВОЧНАЯ"), i => { Assert.Equal("1", i.Quantity); Assert.Equal(new[] { "65" }, i.Weights); });
        var steak = Assert.Single(Lunch.Value.Items, i => i.Name == "БИФШТЕКС КУРИНЫЙ С КАРТОФЕЛЬНЫМИ ДОЛЬКАМИ");
        Assert.Equal(new[] { "140", "100", "40" }, steak.Weights);
        Assert.Contains(Lunch.Value.Items, i => i.Name == "РАССОЛЬНИК ПО-СТАРОРУССКИ ОВОЩНОЙ");
    }

    [Fact]
    public void Unknown_section_is_error_and_is_never_mapped()
    {
        var result = new MenuConverter(new MenuConfiguration(["Суп"])).Convert(Fixture("breakfast.pdf"));
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Severity == Severity.Error && d.Message.Contains("Неизвестный раздел"));
        Assert.Contains(result.Items, i => i.Category == "Горячее блюдо или каша");
    }

    [Fact]
    public void Unknown_time_and_invalid_date_are_rejected()
    {
        var page = PdfTextReader.Read(Fixture("breakfast.pdf"))[0];
        var time = page with { Words = page.Words.Select(w => w.Text == "08:30" ? w with { Text = "09:00" } : w).ToArray() };
        Assert.Throws<FormatException>(() => PageHeaderParser.Parse(time));
        var date = page with { Words = page.Words.Select(w => w.Text == "12.10.26" ? w with { Text = "31.02.26" } : w).ToArray() };
        Assert.Throws<FormatException>(() => PageHeaderParser.Parse(date));
    }

    [Fact]
    public void Rejects_missing_nutrition_or_unrecognized_body_text()
    {
        var pages = PdfTextReader.Read(Fixture("lunch.pdf")).ToArray();
        var first = pages[0];
        pages[0] = first with { Words = first.Words.Append(new TextWord("НЕРАЗОБРАНО", 20, 80, 400, 13, false)).ToArray() };
        Assert.False(new MenuConverter().ConvertPages(pages).Success);
        pages[0] = first with { Words = first.Words.Where(w => !(w.Text == "Б:" && w.X0 < 100 && w.Baseline < 200)).ToArray() };
        Assert.False(new MenuConverter().ConvertPages(pages).Success);
    }

    [Fact]
    public void Rejects_empty_missing_and_non_pdf_files()
    {
        Assert.False(new MenuConverter().Convert("/file-that-does-not-exist.pdf").Success);
        Assert.False(new MenuConverter().Convert(Fixture("encrypted.pdf")).Success);
        Assert.False(new MenuConverter().ConvertPages([]).Success);
        Assert.False(new MenuConverter().ConvertPages([new TextPage(1, 595, 842, [])]).Success);
        var file = Path.GetTempFileName();
        try { Assert.False(new MenuConverter().Convert(file).Success); File.WriteAllText(file, "not a PDF"); Assert.False(new MenuConverter().Convert(file).Success); }
        finally { File.Delete(file); }
    }

    [Fact]
    public void Mixed_meals_and_cancellation_are_rejected()
    {
        var pages = PdfTextReader.Read(Fixture("lunch.pdf")).Concat(PdfTextReader.Read(Fixture("breakfast.pdf"))).ToArray();
        Assert.False(new MenuConverter().ConvertPages(pages).Success);
        Assert.Throws<OperationCanceledException>(() => new MenuConverter().Convert(Fixture("breakfast.pdf"), new CancellationToken(true)));
    }

    [Fact]
    public void Dish_can_continue_on_next_page_and_day_can_have_more_than_two_pages()
    {
        var original = PdfTextReader.Read(Fixture("lunch.pdf")).Take(2).ToArray();
        // Turn each column into a separate page, keeping headers and footers.
        // The sausage name and the drink composition now continue across pages.
        var split = original.SelectMany(page => new[] { false, true }.Select(right => page with
        {
            Words = page.Words.Where(w => w.Baseline < 105 || w.Baseline > 815 || (w.X0 >= page.Width / 2) == right)
                .Select(w => right && w.Baseline is > 105 and < 815 ? w with { X0 = w.X0 - page.Width / 2, X1 = w.X1 - page.Width / 2 } : w).ToArray()
        })).Select((page, i) => page with { Number = i + 1 }).ToArray();
        var result = new MenuConverter().ConvertPages(split);
        Assert.True(result.Success, string.Join("\n", result.Diagnostics));
        var expected = Lunch.Value.Items.Where(i => i.Date == new DateOnly(2026, 10, 12)).ToArray();
        Assert.Equal(31, result.Items.Count);
        for (var i = 0; i < expected.Length; i++)
            Assert.Equal(XlsxMenuWriter.Values(expected[i], Office.Space), XlsxMenuWriter.Values(result.Items[i], Office.Space));
    }
}
