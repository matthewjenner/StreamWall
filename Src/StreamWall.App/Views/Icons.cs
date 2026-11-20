using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

namespace StreamWall.App.Views;

/// <summary>
/// Standard Windows icons (Segoe Fluent Icons on Windows 11, Segoe MDL2 Assets on Windows 10), so
/// strip controls are recognizable icon buttons with a tooltip instead of words. Glyphs are built
/// from code points to keep the source ASCII.
/// </summary>
public static class Icons
{
    public static readonly FontFamily Font = new("Segoe Fluent Icons, Segoe MDL2 Assets");

    public static readonly string Volume = Glyph(0xE767);
    public static readonly string Mute = Glyph(0xE74F);
    public static readonly string Reload = Glyph(0xE72C);
    public static readonly string Swap = Glyph(0xE8AB);
    public static readonly string OpenInNew = Glyph(0xE8A7);
    public static readonly string Close = Glyph(0xE711);
    public static readonly string Add = Glyph(0xE710);
    public static readonly string Back = Glyph(0xE72B);
    public static readonly string Forward = Glyph(0xE72A);
    public static readonly string ZoomIn = Glyph(0xE8A3);
    public static readonly string ZoomOut = Glyph(0xE71F);
    public static readonly string Pin = Glyph(0xE718);
    public static readonly string Pinned = Glyph(0xE840);

    /// <summary>A small, flat icon button with a tooltip (the strip's controls).</summary>
    public static Button Button(string glyph, string tip, string name) => Style(new Button(), glyph, tip, name);

    public static ToggleButton Toggle(string glyph, string tip, string name) => Style(new ToggleButton(), glyph, tip, name);

    private static T Style<T>(T button, string glyph, string tip, string name) where T : Button
    {
        button.Content = glyph;
        button.FontFamily = Font;
        button.FontSize = 13;
        button.Width = 26;
        button.Height = 22;
        button.Padding = new Thickness(0);
        button.Margin = new Thickness(1, 0);
        button.HorizontalContentAlignment = HorizontalAlignment.Center;
        button.VerticalContentAlignment = VerticalAlignment.Center;
        button.Background = Brushes.Transparent;
        button.Name = name;
        ToolTip.SetTip(button, tip);
        return button;
    }

    private static string Glyph(int codePoint) => char.ConvertFromUtf32(codePoint);
}
