using CafeMenuConverter.Core.Diagnostics;
using CafeMenuConverter.Core.Layout;
using CafeMenuConverter.Core.Model;
using CafeMenuConverter.Core.Pdf;

namespace CafeMenuConverter.Core.Parsing;

/// <summary>Reads the known menu template in visual order. Every body word must be consumed once.</summary>
internal sealed class DishAssembler(DiagnosticBag diagnostics)
{
    private sealed class Dish(DateOnly date, string meal, string section, int page)
    {
        public DateOnly Date = date;
        public string Meal = meal, Section = section, Price = "", Weight = "";
        public int Page = page;
        public List<string> Names = [], Composition = [];
        public Nutrition? Nutrition;
    }

    private Dish? current;
    private string section = "";
    public List<MenuItem> Items { get; } = [];

    public void EndDay()
    {
        Finish();
        section = "";
    }

    public void Read(TextPage page, PageHeader header)
    {
        var body = page.Words.Where(w => w.Baseline > header.BodyTop && w.Baseline < header.BodyBottom).ToArray();
        var table = page.Words.Any(w => w.Text == "Белки," && w.Baseline < page.Height * .14);
        if (table) ReadRegion(body, page, header, true);
        else
        {
            ReadRegion(body.Where(w => w.X0 < page.Width / 2).ToArray(), page, header, false);
            ReadRegion(body.Where(w => w.X0 >= page.Width / 2).ToArray(), page, header, false);
        }
    }

    private void ReadRegion(TextWord[] words, TextPage page, PageHeader header, bool table)
    {
        var remaining = new HashSet<TextWord>(words, ReferenceEqualityComparer.Instance);
        var events = new List<(double Y, string Kind, string Text, string Weight, string Price, Nutrition? Nutrition)>();
        void Consume(IEnumerable<TextWord> used)
        {
            foreach (var word in used)
                if (!remaining.Remove(word)) diagnostics.Error($"Слово использовано повторно: {word}", page.Number);
        }

        // The table labels extend a little below the date baseline.
        var labels = words.Where(w => table && w.FontSize == 6 && w.Baseline < page.Height * .14).ToArray();
        Consume(labels);
        foreach (var line in LineBuilder.Build(remaining.Where(w => w.IsWhite)))
        {
            Consume(line.Words);
            events.Add((line.Y, "section", line.Text, "", "", null));
        }

        var names = LineBuilder.Build(remaining.Where(IsName));
        var blocks = new List<List<TextLine>>();
        foreach (var line in names)
        {
            if (blocks.Count == 0 || line.Y - blocks[^1][^1].Y > 17)
                blocks.Add([]);
            blocks[^1].Add(line);
        }
        var centers = table ? TableCenters(page) : [];
        foreach (var block in blocks)
        {
            Consume(block.SelectMany(l => l.Words));
            var low = block[0].Y - 8;
            var high = block[^1].Y + 8;
            var priceWords = remaining.Where(w => Math.Abs(w.FontSize - 10.5) < .1 && w.Baseline >= low && w.Baseline <= high).ToArray();
            Consume(priceWords);
            var priceText = string.Join(" ", priceWords.OrderBy(w => w.X0).Select(w => w.Text));
            var price = "";
            if (priceWords.Length > 0 && !PriceParser.TryParse(priceText, out price))
                diagnostics.Error($"Не удалось разобрать цену «{priceText}».", page.Number);

            var startX = table ? centers[0] - 35 : (block[0].Words[0].X0 < page.Width / 2 ? 0 : page.Width / 2) + page.Width * .32;
            var values = remaining.Where(w => !w.IsWhite && w.FontSize == 9 && w.X0 >= startX && w.Baseline >= low && w.Baseline <= high).ToArray();
            Consume(values);
            string weight;
            Nutrition? nutrition = null;
            if (table)
            {
                var cells = Enumerable.Range(0, 5).Select(i => string.Join(" ", values.Where(w => Nearest(centers, w.CenterX) == i)
                    .OrderBy(w => w.Baseline).ThenBy(w => w.X0).Select(w => w.Text))).ToArray();
                weight = cells[0];
                if (price.Length > 0)
                {
                    if (cells.Skip(1).Any(c => !IsNumber(c))) diagnostics.Error($"Некорректные КБЖУ в таблице: {string.Join(" / ", cells)}", page.Number);
                    nutrition = new Nutrition(cells[1], cells[2], cells[3], cells[4]);
                }
            }
            else weight = string.Join(" ", LineBuilder.Build(values).Select(l => l.Text));
            events.Add((block[0].Y, "name", TextJoin.JoinName(block.Select(l => l.Text)), weight, price, nutrition));
        }
        foreach (var line in LineBuilder.Build(remaining))
        {
            Consume(line.Words);
            if (line.Words.Any(w => w.FontSize != 9))
                diagnostics.Error($"Нераспознанный текст: «{line.Text}», y={line.Y:F1}.", page.Number);
            else events.Add((line.Y, "body", line.Text, "", "", null));
        }
        foreach (var e in events.OrderBy(e => e.Y))
        {
            switch (e.Kind)
            {
                case "section":
                    Finish();
                    section = e.Text;
                    break;
                case "name":
                    // A name can start at the bottom of the preceding column/page.
                    var continuation = current is not null && current.Nutrition is null && current.Composition.Count == 0 &&
                        (e.Price.Length == 0 || current.Price.Length == 0);
                    if (!continuation)
                    {
                        Finish();
                        current = new Dish(header.Date, header.MealType, section, page.Number);
                    }
                    current!.Names.Add(e.Text);
                    if (e.Price.Length > 0) current.Price = e.Price;
                    if (e.Weight.Length > 0)
                    {
                        if (current.Weight.Length > 0) diagnostics.Error("Два значения выхода у одного блюда.", page.Number);
                        current.Weight = e.Weight;
                    }
                    if (e.Nutrition is not null) current.Nutrition = e.Nutrition;
                    break;
                case "body":
                    if (current is null) diagnostics.Error($"Текст вне блюда: «{e.Text}».", page.Number);
                    else if (NutritionParser.TryParse(e.Text, out var nutrition))
                    {
                        if (current.Nutrition is not null) diagnostics.Error("Повторная строка КБЖУ.", page.Number);
                        current.Nutrition = nutrition;
                    }
                    else if (NutritionParser.LooksLikeNutrition(e.Text)) diagnostics.Error($"Не разобрана строка КБЖУ: «{e.Text}».", page.Number);
                    else
                    {
                        if (!table && current.Nutrition is not null) diagnostics.Error($"Лишний текст после КБЖУ: «{e.Text}».", page.Number);
                        current.Composition.Add(e.Text);
                    }
                    break;
            }
        }
    }

    private static bool IsName(TextWord w) => !w.IsWhite && Math.Abs(w.FontSize - 11) < .1;
    private static bool IsNumber(string value) => value.Length > 0 && value.All(char.IsAsciiDigit);
    private static int Nearest(double[] centers, double x) => Enumerable.Range(0, centers.Length).MinBy(i => Math.Abs(centers[i] - x));

    private static double[] TableCenters(TextPage page)
    {
        string[] labels = ["Выход", "Белки,", "Жиры,", "Угле-", "Ккал"];
        return labels.Select(label =>
        {
            var matches = page.Words.Where(w => w.Text == label && w.Baseline < page.Height * .14).ToArray();
            if (matches.Length != 1) throw new FormatException($"Не найдена колонка таблицы «{label}».");
            return matches[0].CenterX;
        }).ToArray();
    }

    private void Finish()
    {
        if (current is not { } d) return;
        current = null;
        var name = TextJoin.JoinName(d.Names);
        if (d.Section.Length == 0) diagnostics.Error($"Не определён раздел для «{name}».", d.Page);
        if (d.Price.Length == 0) diagnostics.Error($"Нет цены у «{name}».", d.Page);
        if (d.Nutrition is null) diagnostics.Error($"Нет КБЖУ у «{name}».", d.Page);
        if (!WeightParser.TryParse(d.Weight, out var weight)) diagnostics.Error($"Неизвестный выход «{d.Weight}» у «{name}».", d.Page);
        var composition = CompositionParser.Parse(d.Composition);
        if (!composition.IsBalanced) diagnostics.Warning($"Непарные скобки в составе «{name}»; текст сохранён.", d.Page);
        var n = d.Nutrition ?? new Nutrition("", "", "", "");
        if (n.IsAllZero)
        {
            diagnostics.Warning($"Все КБЖУ равны нулю у «{name}»; ячейки оставлены пустыми.", d.Page);
            n = new Nutrition("", "", "", "");
        }
        Items.Add(new MenuItem(d.Date, d.Meal, d.Section, name, composition.Items, weight.Weights, weight.Unit, weight.Quantity,
            d.Price, n.Calories, n.Protein, n.Fats, n.Carbohydrates, d.Page));
    }
}
