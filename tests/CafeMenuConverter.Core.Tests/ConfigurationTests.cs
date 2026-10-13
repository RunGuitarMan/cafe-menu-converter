using CafeMenuConverter.Core.Configuration;

namespace CafeMenuConverter.Core.Tests;

public class ConfigurationTests
{
    [Fact]
    public void Missing_override_uses_embedded_defaults()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "categories.json");
        var config = MenuConfiguration.LoadOrDefault(path);
        Assert.Equal(12, config.Categories.Length);
        Assert.True(config.Contains("Горячее блюдо или каша"));
        Assert.True(config.Contains("Азиатский суп"));
    }

    [Fact]
    public void Existing_override_replaces_defaults_instead_of_merging()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "{\"categories\":[\"Новый раздел\"]}");
            var config = MenuConfiguration.LoadOrDefault(path);
            Assert.Equal(["Новый раздел"], config.Categories);
            Assert.False(config.Contains("Суп"));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Malformed_override_does_not_silently_fall_back()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "invalid json");
            Assert.Throws<System.Text.Json.JsonException>(() => MenuConfiguration.LoadOrDefault(path));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Modifying_one_default_instance_does_not_change_future_defaults()
    {
        var config = MenuConfiguration.LoadDefault();
        config.Categories[0] = "Changed";
        Assert.True(MenuConfiguration.LoadDefault().Contains("Горячее блюдо или каша"));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"categories\":[]}")]
    [InlineData("{\"categories\":[\"\"]}")]
    [InlineData("{\"categories\":[\"Суп\",\"СУП\"]}")]
    public void Rejects_incomplete_or_ambiguous_config(string json)
    {
        var path = Path.GetTempFileName();
        try { File.WriteAllText(path, json); Assert.Throws<FormatException>(() => MenuConfiguration.LoadOrDefault(path)); }
        finally { File.Delete(path); }
    }
}
