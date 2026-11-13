namespace StreamWall.Platform;

/// <summary>
/// Where the app keeps its data. <c>%APPDATA%\StreamWall\App\</c> holds config.dat (encrypted)
/// and logs\. The WebView2 profile (cookies, cache) lives in <c>%LOCALAPPDATA%\StreamWallData\WebView</c>:
/// machine-local, so a roaming profile never carries a browser cache around.
/// </summary>
/// <remarks>
/// <para>
/// <b>Never under <c>%LOCALAPPDATA%\StreamWall</c>.</b> That is Velopack's install folder
/// (<see cref="InstallRoot"/>): the installer refuses to install over an existing folder, and
/// uninstalling deletes it whole, which would take the signed-in session with it (found when
/// v0.1.0 would not install). <c>AppPathsTests</c> pins this.
/// </para>
/// <para>
/// Dev copies - every run that is not the Velopack-installed one (Debug or Release from the repo,
/// artifacts/publish) - use <c>StreamWall\Dev</c> and <c>StreamWallData\DevWebView</c> instead, so
/// they never share (and overwrite) the installed app's config or browser profile.
/// </para>
/// </remarks>
public sealed class AppPaths(string root, string webViewRoot)
{
    private static string Roaming => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

    private static string Local => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    /// <summary>Velopack's per-user install folder (packId StreamWall). No data may live in it.</summary>
    public static string InstallRoot => Path.Combine(Local, "StreamWall");

    /// <summary>Machine-local data: the WebView2 profiles (real, dev and demo).</summary>
    public static string LocalDataRoot => Path.Combine(Local, "StreamWallData");

    /// <summary>The demo's own WebView2 profile, never the real one.</summary>
    public static string DemoWebView => Path.Combine(LocalDataRoot, "DemoWebView");

    public static AppPaths ForCurrentUser(bool dev = false) => dev
        ? new(Path.Combine(Roaming, "StreamWall", "Dev"), Path.Combine(LocalDataRoot, "DevWebView"))
        : new(Path.Combine(Roaming, "StreamWall", "App"), Path.Combine(LocalDataRoot, "WebView"));

    public string Root { get; } = root;

    public string Config => Path.Combine(Root, "config.dat");

    public string Logs => Path.Combine(Root, "logs");

    /// <summary>The shared WebView2 user-data folder for every cell and the Browser tab.</summary>
    public string WebView { get; } = webViewRoot;
}
