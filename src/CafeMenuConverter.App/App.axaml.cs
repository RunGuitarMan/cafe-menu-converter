using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace CafeMenuConverter.App;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
            if (desktop.Args is ["--verify-release", var fixtures, var reportDirectory])
                ReleaseVerification.Attach(desktop, fixtures, reportDirectory);
        }
        base.OnFrameworkInitializationCompleted();
    }
}
