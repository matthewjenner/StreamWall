using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using StreamWall.App.Services;
using StreamWall.App.ViewModels;
using StreamWall.App.Views;
using StreamWall.Core.Config;
using StreamWall.Core.Presentation;

namespace StreamWall.App;

public sealed partial class App : Application
{
    private AppHost? _host;
    private WallManager? _walls;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Closing the control panel quits (walls included) until the tray arrives in M6.
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;

            AppConfig? seed = null;
            if (AppArgs.ScreenshotFolder is not null)
            {
                // A windowed wall, so a screenshot run never covers the whole monitor.
                AppConfig demo = AppHost.DemoConfig();
                seed = demo with { Walls = [.. demo.Walls.Select(w => w with { Fullscreen = false })] };
            }
            AppHost host = AppArgs.Demo ? AppHost.CreateDemo(seed) : AppHost.CreateReal();
            _host = host;
            WallManager walls = new(host);
            _walls = walls;
            var viewModel = new MainWindowViewModel(host);
            var window = new MainWindow
            {
                DataContext = viewModel,
                CurrentMap = () => WallMap.From(host.Config),
                AddWall = host.AddWall,
            };
            desktop.MainWindow = window;
            if (AppArgs.ScreenshotFolder is { } folder)
                window.Opened += (_, _) => _ = Screenshots.RunAsync(window, viewModel, host, walls, folder, () => desktop.Shutdown());
            desktop.Exit += (_, _) =>
            {
                _walls?.CloseAll();
                // Off the UI thread: the save's continuations must not need the (exiting) dispatcher.
                Task.Run(host.FlushAsync).Wait(TimeSpan.FromSeconds(5));
                host.Dispose();
            };
            _ = host.StartAsync();
        }
        base.OnFrameworkInitializationCompleted();
    }
}
