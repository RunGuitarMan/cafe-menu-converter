using System.Text.Json;
using CafeMenuConverter.Core.Configuration;

namespace CafeMenuConverter.App;

internal static class AppConfiguration
{
    // Keep mutable settings outside the signed bundle, including when Gatekeeper translocates it.
    public static string OverridePath => OperatingSystem.IsMacOS()
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Library", "Application Support", "CafeMenuConverter", "categories.json")
        : Path.Combine(AppContext.BaseDirectory, "categories.json");

    public static MenuConfiguration Load(string? overridePath = null)
    {
        var path = overridePath ?? OverridePath;
        try { return MenuConfiguration.LoadOrDefault(path); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or FormatException)
        {
            throw new InvalidOperationException($"Не удалось прочитать конфиг категорий «{path}»: {ex.Message}", ex);
        }
    }
}
