using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
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
/// The control panel's real window and view model on the demo host (memory-only config and log),
/// driven like a user would (RingView's pattern; Avalonia.Headless.XUnit is not used). Wall windows
/// and web views are not started: headless has no WebView2.
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

    public string LogText => string.Join("\n", ((MemoryDiagSink)Host.Sink).Lines);

    public static UiHarness Start(AppConfig? seed = null)
    {
        AppHost host = AppHost.CreateDemo(seed);
        var vm = new MainWindowViewModel(host);
        var window = new MainWindow { DataContext = vm, Width = 1180, Height = 760 };
        window.Show();
        _ = host.StartAsync();
        var harness = new UiHarness(host, vm, window);
        harness.WaitUntil(() => !vm.IsLoading, "the config to load");
        harness.Settle();
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

    /// <summary>Lets layout and pending work settle for a while.</summary>
    public void Settle(int ms = 150)
    {
        var until = DateTime.UtcNow.AddMilliseconds(ms);
        while (DateTime.UtcNow < until)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(10);
        }
        Dispatcher.UIThread.RunJobs();
    }

    public T Find<T>(Func<T, bool> match, string what) where T : Visual => FindIn(Window, match, what);

    public T FindIn<T>(TopLevel root, Func<T, bool> match, string? what = null) where T : Visual =>
        root.GetVisualDescendants().OfType<T>().FirstOrDefault(c => c.IsEffectivelyVisible && match(c))
        ?? throw new InvalidOperationException("Not found: " + (what ?? typeof(T).Name));

    public IEnumerable<T> FindAll<T>(Func<T, bool> match) where T : Visual =>
        Window.GetVisualDescendants().OfType<T>().Where(c => c.IsEffectivelyVisible && match(c));

    public T Named<T>(string name) where T : Control => Find<T>(c => c.Name == name, name);

    public TextBlock TextNamed(string name) => Named<TextBlock>(name);

    public void Click(Control control)
    {
        var window = (Window)TopLevel.GetTopLevel(control)!;
        Point at = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)
            ?? throw new InvalidOperationException("Not in a window");
        window.MouseMove(at, RawInputModifiers.None);
        window.MouseDown(at, MouseButton.Left, RawInputModifiers.None);
        window.MouseUp(at, MouseButton.Left, RawInputModifiers.None);
        Settle();
    }

    public void Type(TextBox box, string text)
    {
        Click(box);
        box.Focus();
        Window.KeyTextInput(text);
        Settle(50);
    }

    public void Dispose()
    {
        Window.Close();
        Host.Dispose();
    }
}
