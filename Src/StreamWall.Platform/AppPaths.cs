namespace StreamWall.Platform;

/// <summary>
/// Where the app keeps its data. <c>%APPDATA%\StreamWall\App\</c> holds config.dat (encrypted)
/// and logs\. The WebView2 profile (cookies, cache) lives in <c>%LOCALAPPDATA%\StreamWall\WebView</c>:
/// machine-local, so a roaming profile never carries a browser cache around.
/// </summary>
public sealed class AppPaths(string root, string webViewRoot)
{
    public static AppPaths ForCurrentUser() => new(
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StreamWall", "App"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StreamWall", "WebView"));

    public string Root { get; } = root;

    public string Config => Path.Combine(Root, "config.dat");

    public string Logs => Path.Combine(Root, "logs");

    /// <summary>The shared WebView2 user-data folder for every cell and the sign-in window.</summary>
    public string WebView { get; } = webViewRoot;
}
