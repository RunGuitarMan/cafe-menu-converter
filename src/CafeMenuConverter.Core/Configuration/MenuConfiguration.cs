using System.Text.Json;

namespace CafeMenuConverter.Core.Configuration;

/// <summary>Allowed section headings only. The original spelling is always exported without mapping.</summary>
public sealed record MenuConfiguration(string[] Categories)
{
    public static MenuConfiguration Load(string path)
    {
        var config = JsonSerializer.Deserialize<MenuConfiguration>(File.ReadAllText(path), new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow
        }) ?? throw new FormatException("Пустой конфиг категорий.");
        if (config.Categories is null || config.Categories.Length == 0 || config.Categories.Any(string.IsNullOrWhiteSpace))
            throw new FormatException("В конфиге нужен непустой список categories с заголовками разделов PDF.");
        if (config.Categories.Distinct(StringComparer.OrdinalIgnoreCase).Count() != config.Categories.Length)
            throw new FormatException("В конфиге категорий есть повторяющиеся заголовки.");
        return config;
    }

    public bool Contains(string category) => Categories.Contains(category, StringComparer.OrdinalIgnoreCase);
}
