namespace StreamWall.Core.Config;

/// <summary>
/// Where a window was: position in screen pixels (top-left of the frame), size in device-independent
/// units (client area), and whether it was maximized. A minimized window is never saved; a
/// maximized one keeps its last normal bounds so un-maximizing goes back to them.
/// </summary>
public sealed record WindowPlacement(int X, int Y, double Width, double Height, bool Maximized = false);

/// <summary>A monitor's usable area (screen pixels) and its scaling, for placement checks.</summary>
public sealed record ScreenArea(int X, int Y, int Width, int Height, double Scaling, bool IsPrimary)
{
    public bool Contains(int x, int y) => x >= X && y >= Y && x < X + Width && y < Y + Height;
}

/// <summary>
/// Restoring a window safely: back where it was if it is still reachable; otherwise centered on
/// the primary monitor. Pure, so every case (unplugged monitor, changed resolution, too large,
/// too small, maximized) is unit-tested.
/// </summary>
public static class WindowPlacementRules
{
    /// <summary>How much of the window's top edge (where the title bar is) must be on a monitor, in pixels.</summary>
    public const int GrabWidth = 120;
    public const int GrabHeight = 30;

    /// <summary>
    /// Where to put the window: the saved placement, adjusted to fit; or centered on the primary
    /// monitor (at the saved size if any) when it would be unreachable. Null: use the window's
    /// default placement (nothing saved, or no monitors known).
    /// </summary>
    public static WindowPlacement? Restore(WindowPlacement? saved, IReadOnlyList<ScreenArea> screens, double minWidth, double minHeight)
    {
        if (saved is null || screens.Count == 0)
            return null;

        ScreenArea? home = HomeScreen(saved, screens);
        if (home is not null)
        {
            (double w, double h) = Fit(saved.Width, saved.Height, home, minWidth, minHeight);
            // Keep it on that monitor: shift left/up if the (possibly shrunk) window would hang off.
            int x = Math.Clamp(saved.X, home.X, Math.Max(home.X, home.X + home.Width - Px(w, home)));
            int y = Math.Clamp(saved.Y, home.Y, Math.Max(home.Y, home.Y + home.Height - Px(h, home)));
            return new WindowPlacement(x, y, w, h, saved.Maximized);
        }

        // Unreachable (monitor gone, layout changed): center on the primary monitor.
        ScreenArea primary = screens.FirstOrDefault(s => s.IsPrimary) ?? screens[0];
        (double pw, double ph) = Fit(saved.Width, saved.Height, primary, minWidth, minHeight);
        return new WindowPlacement(
            primary.X + (primary.Width - Px(pw, primary)) / 2,
            primary.Y + (primary.Height - Px(ph, primary)) / 2,
            pw, ph, saved.Maximized);
    }

    /// <summary>A saved placement worth keeping (positive, sensible size); otherwise null.</summary>
    public static WindowPlacement? Normalize(WindowPlacement? placement) =>
        placement is { Width: > 0 and < 100_000, Height: > 0 and < 100_000 } p && !double.IsNaN(p.Width) && !double.IsNaN(p.Height)
            ? p
            : null;

    /// <summary>
    /// The monitor holding most of the window's top edge (where the title bar is), if enough of it
    /// is on a monitor to grab; a window straddling two monitors belongs to the one with more of it.
    /// </summary>
    private static ScreenArea? HomeScreen(WindowPlacement p, IReadOnlyList<ScreenArea> screens)
    {
        ScreenArea? best = null;
        int bestWidth = 0;
        foreach (ScreenArea s in screens)
        {
            int left = Math.Max(p.X, s.X);
            int right = Math.Min(p.X + Px(p.Width, s), s.X + s.Width);
            int top = Math.Max(p.Y, s.Y);
            int bottom = Math.Min(p.Y + GrabHeight, s.Y + s.Height);
            int width = right - left;
            if (width >= GrabWidth && bottom - top >= GrabHeight / 2 && width > bestWidth)
            {
                best = s;
                bestWidth = width;
            }
        }
        return best;
    }

    /// <summary>At least the minimum size, at most the monitor's usable area.</summary>
    private static (double Width, double Height) Fit(double width, double height, ScreenArea s, double minWidth, double minHeight)
    {
        double maxW = s.Width / s.Scaling;
        double maxH = s.Height / s.Scaling;
        return (Math.Clamp(width, Math.Min(minWidth, maxW), maxW), Math.Clamp(height, Math.Min(minHeight, maxH), maxH));
    }

    private static int Px(double dip, ScreenArea s) => (int)Math.Round(dip * s.Scaling);
}
