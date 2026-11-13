namespace StreamWall.Platform;

/// <summary>
/// Where the app keeps its data. <c>%APPDATA%\StreamWall\App\</c> holds config.dat (encrypted)
/// and logs\. The WebView2 profile (cookies, cache) lives in <c>%LOCALAPPDATA%\StreamWall\WebView</c>:
/// machine-local, so a roaming profile never carries a browser cache around.
/// </summary>
/// <remarks>
/// Debug builds use <c>StreamWall\Dev</c> and <c>StreamWall\DevWebView</c> instead, so a dev run
/// next to the installed app never shares (and overwrites) its config or browser profile.
/// </remarks>
public sealed class AppPaths(string root, string webViewRoot)
{
    public static AppPaths ForCurrentUser(bool dev = false)
    {
        string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return dev
            ? new(Path.Combine(roaming, "StreamWall", "Dev"), Path.Combine(local, "StreamWall", "DevWebView"))
            : new(Path.Combine(roaming, "StreamWall", "App"), Path.Combine(local, "StreamWall", "WebView"));
    }

    public string Root { get; } = root;

    public string Config => Path.Combine(Root, "config.dat");

    public string Logs => Path.Combine(Root, "logs");

    /// <summary>The shared WebView2 user-data folder for every cell and the Browser tab.</summary>
    public string WebView { get; } = webViewRoot;
}
