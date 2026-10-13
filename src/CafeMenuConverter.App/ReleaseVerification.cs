using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using CafeMenuConverter.Core.Model;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Validation;

namespace CafeMenuConverter.App;

/// <summary>Opt-in release check using the shipped executable, native window, and normal conversion flow.</summary>
internal static class ReleaseVerification
{
    public static void Attach(IClassicDesktopStyleApplicationLifetime desktop, string fixtures, string reportDirectory)
    {
        var window = desktop.MainWindow!;
        window.Opened += async (_, _) =>
        {
            var exitCode = 1;
            var results = new List<object>();
            string? error = null;
            try
            {
                Directory.CreateDirectory(reportDirectory);
                if (File.Exists(AppConfiguration.OverridePath))
                    throw new InvalidOperationException($"The default-config release check requires no override at {AppConfiguration.OverridePath}.");
                var model = (MainViewModel)window.DataContext!;
                model.SelectedOffice = Office.Oko;
                foreach (var (name, expectedRows) in new[] { ("breakfast", 145), ("lunch", 155) })
                {
                    model.InputPath = Path.Combine(fixtures, name + ".pdf");
                    model.OutputPath = Path.Combine(reportDirectory, name + ".xlsx");
                    await model.ConvertAsync(_ => Task.FromResult(false));
                    if (!model.CanOpenFolder || model.Items.Count != expectedRows)
                        throw new InvalidOperationException($"{name}: {model.Status} {model.Diagnostics}");
                    using var workbook = SpreadsheetDocument.Open(model.SavedPath, false);
                    if (new OpenXmlValidator().Validate(workbook).Any())
                        throw new InvalidOperationException($"{name}: invalid XLSX.");
                    var rows = workbook.WorkbookPart!.WorksheetParts.Single().Worksheet!.Descendants<Row>().Count() - 1;
                    if (rows != expectedRows) throw new InvalidOperationException($"{name}: unexpected XLSX row count {rows}.");
                    results.Add(new { menu = name, rows });
                }
                // Rendering also exercises the bundled fonts, Skia and HarfBuzz on a real platform backend.
                await Task.Delay(500);
                using var image = new RenderTargetBitmap(new PixelSize((int)window.Bounds.Width, (int)window.Bounds.Height));
                image.Render(window);
                image.Save(Path.Combine(reportDirectory, "window.png"));
                exitCode = 0;
            }
            catch (Exception ex) { error = ex.ToString(); }
            try
            {
                Directory.CreateDirectory(reportDirectory);
                using var process = Process.GetCurrentProcess();
                var report = new
                {
                    success = exitCode == 0, error, results,
                    runtime = RuntimeInformation.FrameworkDescription,
                    architecture = RuntimeInformation.ProcessArchitecture.ToString(),
                    os = RuntimeInformation.OSDescription,
                    executable = Environment.ProcessPath,
                    overridePath = AppConfiguration.OverridePath,
                    modules = OperatingSystem.IsMacOS()
                        ? MacModules()
                        : process.Modules.Cast<ProcessModule>().Select(m => m.FileName).ToArray()
                };
                File.WriteAllText(Path.Combine(reportDirectory, "report.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { exitCode = 1; }
            desktop.Shutdown(exitCode);
        };
    }

    // Process.Modules only reports the main executable on macOS; use dyld's loaded-image list.
    private static string[] MacModules() => Enumerable.Range(0, checked((int)DyldImageCount()))
        .Select(i => Marshal.PtrToStringUTF8(DyldImageName((uint)i)) ?? "").ToArray();

    [DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "_dyld_image_count")]
    private static extern uint DyldImageCount();

    [DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "_dyld_get_image_name")]
    private static extern IntPtr DyldImageName(uint index);
}
