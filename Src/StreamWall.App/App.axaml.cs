using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using StreamWall.App.Services;
using StreamWall.App.ViewModels;
using StreamWall.App.Views;

namespace StreamWall.App;

public sealed partial class App : Application
{
    private AppHost? _host;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _host = AppArgs.Demo ? AppHost.CreateDemo() : AppHost.CreateReal();
            var viewModel = new MainWindowViewModel(_host);
            desktop.MainWindow = new MainWindow { DataContext = viewModel };
            desktop.Exit += (_, _) => _host?.Dispose();
            _ = _host.StartAsync();
        }
        base.OnFrameworkInitializationCompleted();
    }
}
