using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Platform;
using Microsoft.Web.WebView2.Core;

namespace StreamWall.App.Web;

/// <summary>
/// The one WebView2 environment every cell and the Browser tab share: the same user-data folder and
/// browser arguments (so cookies are shared and creation never fails on mismatched options), the
/// same profile settings, and in demo mode the demo site's virtual hosts. Hosting is Avalonia's
/// <see cref="NativeWebView"/>; everything else goes through the WebView2 SDK wrapped around its COM
/// pointer (proven in M1, Docs/TECHARCH.md). UI thread only.
/// </summary>
public sealed class WebEnvironment(string userDataFolder, bool demo)
{
    public const string DemoHost = "demo.example.invalid";
    public const string DemoFrameHost = "demo-frame.example.invalid";

    /// <summary>Keep playback going while the app is unfocused or covered (Docs/TECHARCH.md).</summary>
    public const string BrowserArguments =
        "--autoplay-policy=no-user-gesture-required --disable-background-timer-throttling " +
        "--disable-renderer-backgrounding --disable-backgrounding-occluded-windows";

    public string UserDataFolder { get; } = userDataFolder;

    public bool Demo { get; } = demo;

    public static string DemoHome => $"https://{DemoHost}/index.html";

    /// <summary>
    /// False in headless UI tests (no WebView2 there): views that would host a web view show
    /// nothing instead, and everything around them still runs.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Wires a web view to the shared environment. <paramref name="ready"/> runs on the UI thread
    /// once the WebView2 behind it exists, with the SDK object to drive it and the raw
    /// ICoreWebView2Controller pointer (for zoom, which the SDK cannot wrap from Avalonia's pointer).
    /// </summary>
    public void Attach(NativeWebView view, Func<CoreWebView2, IntPtr, Task> ready, Action<Exception> failed)
    {
        view.EnvironmentRequested += (_, e) =>
        {
            if (e is WindowsWebView2EnvironmentRequestedEventArgs w)
            {
                w.UserDataFolder = UserDataFolder;
                w.AdditionalBrowserArguments = BrowserArguments;
            }
        };
        // Avalonia's own new-window event: never let it open anything; the SDK handler decides.
        view.NewWindowRequested += (_, e) => e.Handled = true;
        view.AdapterCreated += async (_, e) =>
        {
            try
            {
                if (e.TryGetPlatformHandle() is not IWindowsWebView2PlatformHandle handle)
                    throw new InvalidOperationException("WebView2 is not available on this system.");
                CoreWebView2 core = CoreWebView2.CreateFromComICoreWebView2(handle.CoreWebView2);
                Configure(core);
                await ready(core, handle.CoreWebView2Controller);
            }
            catch (Exception ex)
            {
                failed(ex);
            }
        };
    }

    private void Configure(CoreWebView2 core)
    {
        core.Settings.IsPasswordAutosaveEnabled = false;
        core.Settings.IsGeneralAutofillEnabled = false;
        core.Settings.IsStatusBarEnabled = false;
        core.Profile.IsPasswordAutosaveEnabled = false;
        core.Profile.IsGeneralAutofillEnabled = false;

        if (Demo)
        {
            string site = Path.Combine(AppContext.BaseDirectory, "Web", "DemoSite");
            core.SetVirtualHostNameToFolderMapping(DemoHost, site, CoreWebView2HostResourceAccessKind.Allow);
            core.SetVirtualHostNameToFolderMapping(DemoFrameHost, site, CoreWebView2HostResourceAccessKind.Allow);
        }
    }
}

/// <summary>The scripts every cell injects at document creation, with the cell's settings in front.</summary>
public static class CellScripts
{
    private static readonly string VideoOnly = Load("video-only.js");
    private static readonly string Probe = Load("probe.js");

    public static string For(string? selector, double volume) =>
        "window.__sw = " + JsonSerializer.Serialize(new { selector, volume }) + ";\n"
        + VideoOnly + "\n" + Probe;

    private static string Load(string name)
    {
        using Stream s = typeof(CellScripts).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException("Missing embedded script " + name);
        using var reader = new StreamReader(s);
        return reader.ReadToEnd();
    }
}
