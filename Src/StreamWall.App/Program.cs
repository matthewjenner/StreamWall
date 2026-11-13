using Avalonia;
using StreamWall.Platform;
using Velopack;

namespace StreamWall.App;

internal static class Program
{
    /// <summary>The running instance's guard, listened on by <see cref="App"/>; null for demo runs.</summary>
    public static SingleInstance? Instance { get; private set; }

    /// <summary>
    /// Debug builds are dev copies: their own data folders (<see cref="AppPaths"/>) and single-instance
    /// names, so they run next to the installed app without touching it.
    /// </summary>
    public static bool IsDevBuild =>
#if DEBUG
        true;
#else
        false;
#endif

    // Nothing that needs Avalonia or a SynchronizationContext before StartWithClassicDesktopLifetime.
    [STAThread]
    public static void Main(string[] args)
    {
        // First, exactly once: Velopack's install/update/uninstall hooks run here and exit.
        VelopackApp.Build().Run();

        AppArgs.Parse(args);

        // One StreamWall at a time (per build kind): a second launch shows the running control
        // panel instead. Demo runs touch no real data, so they skip the guard.
        if (!AppArgs.Demo)
        {
            Instance = SingleInstance.ForThisBuild(debug: IsDevBuild);
            if (!Instance.TryAcquire())
            {
                Instance.SignalActivate();
                Instance.Dispose();
                return;
            }
        }

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            Instance?.Dispose();
        }
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
/// Command-line switches, all for development: <c>--demo</c> runs against the local demo site with
/// in-memory config and a demo-only browser profile, never the real ones; <c>--screenshot
/// &lt;folder&gt;</c> (with <c>--demo</c>) drives Send to wall, saves PNGs and quits.
/// </summary>
internal static class AppArgs
{
    public static bool Demo { get; private set; }

    public static string? ScreenshotFolder { get; private set; }

    public static void Parse(string[] args)
    {
        Demo = args.Contains("--demo");
        int shot = Array.IndexOf(args, "--screenshot");
        if (Demo && shot >= 0 && shot + 1 < args.Length)
            ScreenshotFolder = args[shot + 1];
    }
}
