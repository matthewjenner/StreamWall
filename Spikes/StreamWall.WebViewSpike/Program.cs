using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;

namespace StreamWall.WebViewSpike;

// Usage:
//   checks mode (default): capability checks on 8 cells with different test pages, ~36 s
//     StreamWall.WebViewSpike.exe --mode checks [--report <dir>]
//   load mode: N cells of one page, with process/GPU sampling and optional occlusion phases
//     StreamWall.WebViewSpike.exe --mode load --cells 12 --flags all|autoplay|none [--occlude] [--report <dir>]
//   Cells are host-muted unless --audible is given.
internal static class Program
{
    public static SpikeOptions Options { get; private set; } = new();

    [STAThread]
    public static int Main(string[] args)
    {
        Options = SpikeOptions.Parse(args);
        return AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace().StartWithClassicDesktopLifetime(args);
    }
}

internal sealed class App : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        RequestedThemeVariant = ThemeVariant.Dark;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new SpikeWindow(Program.Options, desktop);
        }
        base.OnFrameworkInitializationCompleted();
    }
}

internal sealed record SpikeOptions
{
    public string Mode { get; init; } = "checks";
    public int Cells { get; init; } = 12;
    public string Page { get; init; } = "single.html";
    public string Flags { get; init; } = "all";
    public bool Occlude { get; init; }

    /// <summary>Cells are host-muted (CoreWebView2.IsMuted) unless --audible is passed.</summary>
    public bool Audible { get; init; }
    public string ReportDir { get; init; } = Path.Combine(Path.GetTempPath(), "StreamWallSpike");

    public static string UserDataFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StreamWallData", "Spike", "WebView");

    public string BrowserArguments => Flags switch
    {
        "all" => "--autoplay-policy=no-user-gesture-required --disable-background-timer-throttling " +
                 "--disable-renderer-backgrounding --disable-backgrounding-occluded-windows",
        "autoplay" => "--autoplay-policy=no-user-gesture-required",
        _ => "",
    };

    public static SpikeOptions Parse(string[] args)
    {
        var o = new SpikeOptions();
        for (int i = 0; i < args.Length; i++)
        {
            string next = i + 1 < args.Length ? args[i + 1] : "";
            switch (args[i])
            {
                case "--mode": o = o with { Mode = next }; i++; break;
                case "--cells": o = o with { Cells = int.Parse(next) }; i++; break;
                case "--page": o = o with { Page = next }; i++; break;
                case "--flags": o = o with { Flags = next }; i++; break;
                case "--report": o = o with { ReportDir = next }; i++; break;
                case "--occlude": o = o with { Occlude = true }; break;
                case "--audible": o = o with { Audible = true }; break;
            }
        }
        return o;
    }
}
