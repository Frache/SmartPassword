using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using SmartPassword.App.Services;
using SmartPassword.App.ViewModels;
using SmartPassword.App.Views;

namespace SmartPassword.App;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow();
            var clipboard = new SecureClipboard(window.Clipboard);
            var main = new MainViewModel(new DialogService(window), clipboard);
            window.DataContext = main;
            window.Closing += (_, _) => main.Lock();
            desktop.MainWindow = window;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
