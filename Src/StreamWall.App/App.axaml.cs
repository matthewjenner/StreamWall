using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
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
            // Closing the control panel quits (walls included) until the tray arrives.
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;

            AppConfig? seed = null;
            if (AppArgs.ScreenshotFolder is not null)
            {
                // A windowed wall, so a screenshot run never covers the whole monitor.
                AppConfig demo = AppHost.DemoConfig();
                seed = demo with { Walls = [.. demo.Walls.Select(w => w with { Fullscreen = false })] };
            }
            AppHost host = AppArgs.Demo ? AppHost.CreateDemo(seed) : AppHost.CreateReal(dev: Program.IsDevCopy);
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
            Shutdown.Wire(window, walls, host);

            // A second launch asks this one to show its control panel.
            var activation = new CancellationTokenSource();
            _ = Program.Instance?.ListenAsync(() => Dispatcher.UIThread.Post(() => Show(window)), activation.Token);

            desktop.Exit += (_, _) =>
            {
                activation.Cancel();
                host.Dispose();
            };
            _ = host.StartAsync();
        }
        base.OnFrameworkInitializationCompleted();
    }

    private static void Show(Window window)
    {
        window.Show();
        if (window.WindowState == WindowState.Minimized)
            window.WindowState = WindowState.Normal;
        window.Activate();
    }
}
