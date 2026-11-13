using Avalonia;

namespace StreamWall.App;

internal static class Program
{
    // Nothing that needs Avalonia or a SynchronizationContext before StartWithClassicDesktopLifetime.
    // Velopack's hooks and the single-instance guard join here in M6.
    [STAThread]
    public static void Main(string[] args)
    {
        AppArgs.Parse(args);
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Also used by the visual designer.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .LogToTrace();
}

/// <summary>
/// Command-line switches, all for development: <c>--demo</c> runs with in-memory storage and
/// placeholder streams, never the real config.
/// </summary>
internal static class AppArgs
{
    public static bool Demo { get; private set; }

    public static void Parse(string[] args) => Demo = args.Contains("--demo");
}
