using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using StreamWall.App.Services;
using StreamWall.App.ViewModels;
using StreamWall.App.Views;
using StreamWall.Core.Config;

namespace StreamWall.App.Tests;

/// <summary>The real App (styles) on Avalonia's headless platform with Skia drawing.</summary>
public static class TestApp
{
    private static readonly Lazy<HeadlessUnitTestSession> Session = new(() => HeadlessUnitTestSession.StartNew(typeof(TestApp)));

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UseSkia()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });

    /// <summary>Runs a test body on the headless UI thread (what [AvaloniaFact] would do).</summary>
    public static Task Run(Action test) => Session.Value.Dispatch(test, TestContext.Current.CancellationToken);
}

/// <summary>
/// The app's real window and view model on the demo host (memory-only config and log), driven like
/// a user would (RingView's pattern; Avalonia.Headless.XUnit is not used).
/// </summary>
internal sealed class UiHarness : IDisposable
{
    private UiHarness(AppHost host, MainWindowViewModel vm, MainWindow window)
    {
        Host = host;
        ViewModel = vm;
        Window = window;
    }

    public AppHost Host { get; }
    public MainWindowViewModel ViewModel { get; }
    public MainWindow Window { get; }

    public static UiHarness Start(AppConfig? seed = null)
    {
        AppHost host = AppHost.CreateDemo(seed);
        var vm = new MainWindowViewModel(host);
        var window = new MainWindow { DataContext = vm, Width = 900, Height = 600 };
        window.Show();
        _ = host.StartAsync();
        var harness = new UiHarness(host, vm, window);
        harness.WaitUntil(() => !vm.IsLoading, "the config to load");
        return harness;
    }

    /// <summary>Pumps the UI thread until <paramref name="condition"/> holds.</summary>
    public void WaitUntil(Func<bool> condition, string what, int timeoutMs = 15_000)
    {
        var until = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (true)
        {
            Dispatcher.UIThread.RunJobs();
            if (condition())
                return;
            if (DateTime.UtcNow > until)
                throw new TimeoutException("Timed out waiting for " + what);
            Thread.Sleep(10);
        }
    }

    public T Find<T>(Func<T, bool> match, string what) where T : Visual =>
        Window.GetVisualDescendants().OfType<T>().FirstOrDefault(c => c.IsEffectivelyVisible && match(c))
        ?? throw new InvalidOperationException("Not found: " + what);

    public TextBlock TextNamed(string name) => Find<TextBlock>(t => t.Name == name, "text " + name);

    public void Dispose()
    {
        Window.Close();
        Host.Dispose();
    }
}
