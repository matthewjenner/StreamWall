using Avalonia.Controls;
using Avalonia.Platform;

namespace StreamWall.App.Views;

/// <summary>
/// The app icon for every window (title bar, taskbar, Alt+Tab): the control panel, walls, the
/// picker and dialogs. The exe's own icon comes from the csproj's ApplicationIcon. Regenerate both
/// with Scripts/gen-icons.sh.
/// </summary>
public static class AppIcon
{
    public static readonly Uri Source = new("avares://StreamWall/Assets/icon.ico");

    private static readonly Lazy<WindowIcon?> Loaded = new(() =>
    {
        try
        {
            using Stream stream = AssetLoader.Open(Source);
            return new WindowIcon(stream);
        }
        catch (Exception ex) when (ex is FileNotFoundException or InvalidOperationException or IOException)
        {
            return null; // a missing icon must never stop a window opening
        }
    });

    public static WindowIcon? Window => Loaded.Value;

    /// <summary>Gives a window the app icon.</summary>
    public static T Apply<T>(T window) where T : Window
    {
        window.Icon = Window;
        return window;
    }
}
