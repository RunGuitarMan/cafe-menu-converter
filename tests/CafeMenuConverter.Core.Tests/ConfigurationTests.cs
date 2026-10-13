using CafeMenuConverter.Core.Configuration;

namespace CafeMenuConverter.Core.Tests;

public class ConfigurationTests
{
    [Theory]
    [InlineData("{}")]
    [InlineData("{\"categories\":[]}")]
    [InlineData("{\"categories\":[\"\"]}")]
    [InlineData("{\"categories\":[\"Суп\",\"СУП\"]}")]
    public void Rejects_incomplete_or_ambiguous_config(string json)
    {
        var path = Path.GetTempFileName();
        try { File.WriteAllText(path, json); Assert.Throws<FormatException>(() => MenuConfiguration.Load(path)); }
        finally { File.Delete(path); }
    }
}
