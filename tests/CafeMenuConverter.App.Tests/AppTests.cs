using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using CafeMenuConverter.App;
using CafeMenuConverter.Core.Model;

[assembly: AvaloniaTestApplication(typeof(CafeMenuConverter.App.Tests.TestAppBuilder))]

namespace CafeMenuConverter.App.Tests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UseSkia()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}

public class AppTests
{
    [AvaloniaFact]
    public void Window_loads_and_requires_office_selection()
    {
        var window = new MainWindow();
        window.Show();
        var model = Assert.IsType<MainViewModel>(window.DataContext);
        Assert.Null(model.SelectedOffice);
        Assert.False(window.FindControl<Button>("ConvertButton")!.IsEnabled);
        Assert.Equal(3, model.Offices.Count);
        model.InputPath = Path.Combine(AppContext.BaseDirectory, "breakfast.pdf");
        Assert.False(model.CanConvert);
        model.SelectedOffice = Office.Oko;
        Assert.True(model.CanConvert);
        window.Close();
    }

    [Fact]
    public async Task Conversion_saves_and_declined_overwrite_preserves_file()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ui-menu-{Guid.NewGuid():N}.xlsx");
        var model = new MainViewModel(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "categories.json"))
            { InputPath = Path.Combine(AppContext.BaseDirectory, "breakfast.pdf"), SelectedOffice = Office.Oko, OutputPath = path };
        try
        {
            await model.ConvertAsync(_ => throw new Exception("No overwrite expected"));
            Assert.True(model.CanOpenFolder, model.Diagnostics);
            Assert.Equal(145, model.Items.Count);
            Assert.Contains("ОКО", model.Status);
            Assert.Contains("12.10.2026", model.Summary);
            var previous = File.ReadAllBytes(path);
            var asked = false;
            await model.ConvertAsync(_ => { asked = true; return Task.FromResult(false); });
            Assert.True(asked);
            Assert.Equal(previous, File.ReadAllBytes(path));
            Assert.Contains("отменено", model.Status);
            Assert.False(model.IsBusy);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task Existing_override_is_used_and_invalid_override_blocks_conversion()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"config-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var config = Path.Combine(directory, "categories.json");
        var output = Path.Combine(directory, "output.xlsx");
        var model = new MainViewModel(config)
            { InputPath = Path.Combine(AppContext.BaseDirectory, "breakfast.pdf"), SelectedOffice = Office.Oko, OutputPath = output };
        try
        {
            File.WriteAllText(config, "{\"categories\":[\"Только новый раздел\"]}");
            await model.ConvertAsync(_ => Task.FromResult(false));
            Assert.Contains("Неизвестный раздел", model.Diagnostics);
            Assert.False(File.Exists(output));
            File.WriteAllText(config, "{}");
            await model.ConvertAsync(_ => Task.FromResult(false));
            Assert.Contains("непустой список", model.Diagnostics);
            Assert.Contains(config, model.Diagnostics);
            Assert.False(File.Exists(output));
            File.Delete(config);
            await model.ConvertAsync(_ => Task.FromResult(false));
            Assert.True(model.CanOpenFolder, model.Diagnostics);
            Assert.Equal(145, model.Items.Count);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task Bad_destination_is_reported_and_controls_reenabled()
    {
        var model = new MainViewModel { InputPath = Path.Combine(AppContext.BaseDirectory, "breakfast.pdf"), SelectedOffice = Office.Space,
            OutputPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing-directory.xlsx") };
        await model.ConvertAsync(_ => Task.FromResult(true));
        Assert.False(model.IsBusy);
        Assert.False(model.CanOpenFolder);
        Assert.NotEmpty(model.Diagnostics);
    }
}
