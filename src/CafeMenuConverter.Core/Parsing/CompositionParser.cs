using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace CafeMenuConverter.Core.Parsing;

public sealed record CompositionResult(IReadOnlyList<string> Items, bool IsBalanced);

/// <summary>
/// Состав блюда: строки склеиваются, затем текст делится по запятым, которые стоят вне скобок
/// и не между цифрами («3,2%», «82,5%», «3,25»). Текст элементов не меняется.
/// </summary>
public static class CompositionParser
{
    public static CompositionResult Parse(IEnumerable<string> lines)
    {
        var text = TextJoin.JoinComposition(lines);
        var items = new List<string>();
        var current = new StringBuilder();
        var depth = 0;
        var balanced = true;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            switch (c)
            {
                case '(':
                    depth++;
                    break;
                case ')':
                    depth--;
                    if (depth < 0)
                    {
                        balanced = false;
                        depth = 0;
                    }

                    break;
                case ',' when depth == 0 && !IsDecimalComma(text, i):
                    Add();
                    continue;
            }

            current.Append(c);
        }

        Add();
        if (depth != 0)
            balanced = false;
        return new CompositionResult(items, balanced);

        void Add()
        {
            var item = current.ToString().Trim();
            if (item.Length > 0)
                items.Add(item);
            current.Clear();
        }
    }

    private static bool IsDecimalComma(string text, int index) =>
        index > 0 && index + 1 < text.Length && char.IsDigit(text[index - 1]) && char.IsDigit(text[index + 1]);
}

/// <summary>JSON-массив строк в формате примера: ["a","b"], кириллица не экранируется. Пустой список — пустая строка.</summary>
public static class JsonList
{
    private static readonly JsonSerializerOptions Options = new()
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        WriteIndented = false,
    };

    public static string Serialize(IReadOnlyList<string> items) =>
        items.Count == 0 ? "" : JsonSerializer.Serialize(items, Options);
}
