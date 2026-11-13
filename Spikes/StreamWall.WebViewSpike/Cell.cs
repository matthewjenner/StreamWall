using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Platform;
using Microsoft.Web.WebView2.Core;

namespace StreamWall.WebViewSpike;

internal sealed record Sample(
    double T, bool Top, string Vis, int Raf, bool HasTarget, double Played, bool? Paused, double? Vol,
    bool? Muted, long? Dropped, long? Frames, int? Vw, bool Cookie, bool? SignedOut, int Videos, int Iframes);

/// <summary>One cell: an Avalonia NativeWebView, wrapped with the WebView2 SDK once its adapter exists.</summary>
internal sealed class Cell
{
    public const string SiteHost = "site.example.invalid";
    public const string FrameHost = "frame.example.invalid";

    private readonly SpikeOptions _options;
    private readonly Report _report;
    private readonly string _runId;
    private readonly string _signedOutSelector;

    public Cell(int index, string page, double volume, SpikeOptions options, Report report, string runId)
    {
        Index = index;
        Page = page;
        Volume = volume;
        _options = options;
        _report = report;
        _runId = runId;
        _signedOutSelector = "#login-form";

        View.EnvironmentRequested += OnEnvironmentRequested;
        View.AdapterCreated += OnAdapterCreated;
        View.NewWindowRequested += (_, e) => { e.Handled = true; Event("new-window-blocked-avalonia"); };
        View.ProcessTerminated += (_, _) => Event("process-terminated-avalonia");
    }

    public int Index { get; }
    public string Page { get; }
    public double Volume { get; }
    public NativeWebView View { get; } = new();
    public CoreWebView2? Core { get; private set; }
    public List<Sample> Samples { get; } = [];
    public List<(double T, string Name)> Events { get; } = [];
    public string? AdapterError { get; private set; }
    public double? FailedAt { get; private set; }

    public void Event(string name) => Events.Add((_report.Now, name));

    private void OnEnvironmentRequested(object? sender, WebViewEnvironmentRequestedEventArgs e)
    {
        if (e is WindowsWebView2EnvironmentRequestedEventArgs w)
        {
            w.UserDataFolder = SpikeOptions.UserDataFolder;
            w.AdditionalBrowserArguments = _options.BrowserArguments;
            Event("environment-options-set");
        }
        else
        {
            Event("environment-args-not-webview2:" + e.GetType().Name);
        }
    }

    private async void OnAdapterCreated(object? sender, WebViewAdapterEventArgs e)
    {
        try
        {
            if (e.TryGetPlatformHandle() is not IWindowsWebView2PlatformHandle handle)
            {
                AdapterError = "platform handle is not WebView2";
                return;
            }

            var core = CoreWebView2.CreateFromComICoreWebView2(handle.CoreWebView2);
            Core = core;
            Event("sdk-wrap-ok");

            // Silent by default. Host mute is outside the page, so the injected volume logic
            // still runs and is still measured.
            core.IsMuted = !_options.Audible;

            core.Settings.IsPasswordAutosaveEnabled = false;
            core.Settings.IsGeneralAutofillEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
            core.Profile.IsPasswordAutosaveEnabled = false;
            core.Profile.IsGeneralAutofillEnabled = false;

            string site = Path.Combine(AppContext.BaseDirectory, "TestSite");
            core.SetVirtualHostNameToFolderMapping(SiteHost, site, CoreWebView2HostResourceAccessKind.Allow);
            core.SetVirtualHostNameToFolderMapping(FrameHost, site, CoreWebView2HostResourceAccessKind.Allow);

            core.NewWindowRequested += (_, a) => { a.Handled = true; Event("new-window-blocked-sdk"); };
            core.ProcessFailed += OnProcessFailed;
            core.WebMessageReceived += (_, a) => OnMessage(a.TryGetWebMessageAsString());
            core.FrameCreated += (_, a) =>
            {
                Event("frame-created");
                a.Frame.WebMessageReceived += (_, fa) => OnMessage(fa.TryGetWebMessageAsString());
            };
            core.NavigationStarting += (_, a) =>
            {
                // Signed-out URL pattern check. The URI itself is never logged.
                if (a.Uri.Contains("/login", StringComparison.OrdinalIgnoreCase)) Event("signed-out-url-match");
            };

            string cfg = JsonSerializer.Serialize(new
            {
                cell = Index,
                volume = Volume,
                runId = _runId,
                signedOutSelector = _signedOutSelector,
            });
            await core.AddScriptToExecuteOnDocumentCreatedAsync(
                "window.__sw = " + cfg + ";\n" + Scripts.VideoOnly + "\n" + Scripts.Probe);

            core.Navigate($"https://{SiteHost}/{Page}");
            Event("navigated");
        }
        catch (Exception ex)
        {
            AdapterError = ex.GetType().Name + ": " + ex.Message;
        }
    }

    private void OnProcessFailed(object? sender, CoreWebView2ProcessFailedEventArgs e)
    {
        Event("process-failed:" + e.ProcessFailedKind);
        FailedAt = _report.Now;
        if (e.ProcessFailedKind is CoreWebView2ProcessFailedKind.RenderProcessExited
            or CoreWebView2ProcessFailedKind.RenderProcessUnresponsive)
        {
            Core?.Reload();
            Event("reloaded-after-failure");
        }
    }

    private void OnMessage(string? json)
    {
        if (json is null) return;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var r = doc.RootElement;
            string kind = r.GetProperty("k").GetString() ?? "";
            if (kind == "event")
            {
                string name = r.GetProperty("name").GetString() ?? "";
                string tag = r.TryGetProperty("tag", out var t) ? ":" + t.GetString() : "";
                string where = r.GetProperty("top").GetBoolean() ? "top" : "frame";
                Event($"js:{name}{tag}@{where}");
                return;
            }
            if (kind != "probe") return;

            Samples.Add(new Sample(
                _report.Now,
                r.GetProperty("top").GetBoolean(),
                r.GetProperty("vis").GetString() ?? "",
                r.GetProperty("raf").GetInt32(),
                r.GetProperty("hasTarget").GetBoolean(),
                r.GetProperty("played").GetDouble(),
                Bool(r, "paused"),
                Num(r, "vol"),
                Bool(r, "muted"),
                (long?)Num(r, "dropped"),
                (long?)Num(r, "frames"),
                (int?)Num(r, "vw"),
                r.GetProperty("cookie").GetBoolean(),
                Bool(r, "signedOut"),
                r.GetProperty("videos").GetInt32(),
                r.GetProperty("iframes").GetInt32()));
        }
        catch (Exception ex)
        {
            Event("bad-message:" + ex.GetType().Name);
        }
    }

    private static bool? Bool(JsonElement r, string name) =>
        r.TryGetProperty(name, out var v) && v.ValueKind is JsonValueKind.True or JsonValueKind.False ? v.GetBoolean() : null;

    private static double? Num(JsonElement r, string name) =>
        r.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : null;
}

internal static class Scripts
{
    public static readonly string VideoOnly = Load("video-only.js");
    public static readonly string Probe = Load("probe.js");

    private static string Load(string name)
    {
        using var s = typeof(Scripts).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException("missing resource " + name);
        using var r = new StreamReader(s);
        return r.ReadToEnd();
    }
}
