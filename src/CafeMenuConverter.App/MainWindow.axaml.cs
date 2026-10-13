using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Platform.Storage;

namespace CafeMenuConverter.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel model = new();
    public MainWindow()
    {
        InitializeComponent();
        DataContext = model;
        DropArea.AddHandler(DragDrop.DragOverEvent, DragOver);
        DropArea.AddHandler(DragDrop.DropEvent, Drop);
        Closing += (_, e) => { if (model.IsBusy) { e.Cancel = true; model.Cancel(); } };
    }
    private static readonly FilePickerFileType PdfType = new("PDF меню") { Patterns = ["*.pdf"], MimeTypes = ["application/pdf"] };
    private static readonly FilePickerFileType XlsxType = new("Excel") { Patterns = ["*.xlsx"] };

    private async void ChoosePdf(object? sender, RoutedEventArgs e)
    {
        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Выберите PDF меню", AllowMultiple = false, FileTypeFilter = [PdfType] });
            if (files.Count == 1 && files[0].TryGetLocalPath() is { } path) model.InputPath = path;
        }
        catch (Exception ex) { model.ReportError(ex.Message); }
    }
    private async void ChooseOutput(object? sender, RoutedEventArgs e)
    {
        try
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Сохранить таблицу", SuggestedFileName = Path.GetFileName(model.OutputPath),
                DefaultExtension = "xlsx", FileTypeChoices = [XlsxType], ShowOverwritePrompt = false
            });
            if (file?.TryGetLocalPath() is { } path) model.OutputPath = path;
        }
        catch (Exception ex) { model.ReportError(ex.Message); }
    }
    private void DragOver(object? sender, DragEventArgs e)
    {
        var files = e.DataTransfer.TryGetFiles()?.ToArray();
        e.DragEffects = model.IsIdle && files is { Length: 1 } && IsPdf(files[0].TryGetLocalPath()) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }
    private void Drop(object? sender, DragEventArgs e)
    {
        var files = e.DataTransfer.TryGetFiles()?.ToArray();
        if (model.IsIdle && files is { Length: 1 } && files[0].TryGetLocalPath() is { } path && IsPdf(path)) model.InputPath = path;
        e.Handled = true;
    }
    private static bool IsPdf(string? path) => path is not null && Path.GetExtension(path).Equals(".pdf", StringComparison.OrdinalIgnoreCase);
    private async void Convert(object? sender, RoutedEventArgs e) => await model.ConvertAsync(ConfirmOverwrite);
    private void Cancel(object? sender, RoutedEventArgs e) => model.Cancel();
    private void OpenFolder(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (model.CanOpenFolder) Process.Start(new ProcessStartInfo(Path.GetDirectoryName(model.SavedPath)!) { UseShellExecute = true });
        }
        catch (Exception ex) { model.ReportError($"Не удалось открыть папку: {ex.Message}"); }
    }
    private async Task<bool> ConfirmOverwrite(string path)
    {
        var dialog = new Window { Title = "Заменить файл?", Width = 540, SizeToContent = SizeToContent.Height,
            CanResize = false, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var yes = new Button { Content = "Заменить", Classes = { "accent" } };
        var no = new Button { Content = "Отмена", IsCancel = true };
        yes.Click += (_, _) => dialog.Close(true);
        no.Click += (_, _) => dialog.Close(false);
        dialog.Content = new StackPanel
        {
            Margin = new Thickness(24), Spacing = 20,
            Children =
            {
                new TextBlock { Text = $"Файл уже существует:\n{path}\n\nЗаменить его новой таблицей?", TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 12, Children = { no, yes } }
            }
        };
        return await dialog.ShowDialog<bool>(this);
    }
}
