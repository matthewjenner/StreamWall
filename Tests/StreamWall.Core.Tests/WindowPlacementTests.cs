using StreamWall.Core.Config;

namespace StreamWall.Core.Tests;

/// <summary>Restoring the control panel safely: every off-screen, size and monitor case.</summary>
public sealed class WindowPlacementTests
{
    private const double MinW = 820, MinH = 520;

    // A 2560x1440 primary (taskbar: 1400 high usable) and a 1920x1080 monitor to its left at 100%.
    private static readonly ScreenArea Primary = new(0, 0, 2560, 1400, 1.0, IsPrimary: true);
    private static readonly ScreenArea Left = new(-1920, 0, 1920, 1040, 1.0, IsPrimary: false);
    private static readonly ScreenArea[] Both = [Primary, Left];

    [Fact]
    public void NothingSavedUsesTheDefault() =>
        WindowPlacementRules.Restore(null, Both, MinW, MinH).Should().BeNull();

    [Fact]
    public void NoMonitorsKnownUsesTheDefault() =>
        WindowPlacementRules.Restore(new WindowPlacement(10, 10, 1000, 700), [], MinW, MinH).Should().BeNull();

    [Fact]
    public void AWindowStillOnScreenGoesBackExactly() =>
        WindowPlacementRules.Restore(new WindowPlacement(300, 200, 1180, 760), Both, MinW, MinH)
            .Should().Be(new WindowPlacement(300, 200, 1180, 760));

    [Fact]
    public void AWindowOnAMonitorToTheLeftStaysThere() =>
        WindowPlacementRules.Restore(new WindowPlacement(-1700, 100, 1200, 800), Both, MinW, MinH)
            .Should().Be(new WindowPlacement(-1700, 100, 1200, 800));

    [Fact]
    public void AWindowOnAnUnpluggedMonitorIsCenteredOnThePrimary()
    {
        WindowPlacement placed = WindowPlacementRules.Restore(new WindowPlacement(-1700, 100, 1200, 800), [Primary], MinW, MinH)!;
        placed.Should().Be(new WindowPlacement((2560 - 1200) / 2, (1400 - 800) / 2, 1200, 800));
    }

    [Fact]
    public void AWindowWhoseTitleBarIsOffScreenIsCentered()
    {
        // Top edge above every monitor: the title bar cannot be grabbed.
        WindowPlacement placed = WindowPlacementRules.Restore(new WindowPlacement(300, -500, 1180, 760), Both, MinW, MinH)!;
        (placed.X, placed.Y).Should().Be(((2560 - 1180) / 2, (1400 - 760) / 2));
    }

    [Fact]
    public void AWindowMostlyOffTheRightEdgeIsCentered()
    {
        // Only 50 px of the title bar left on the monitor: not enough to grab.
        WindowPlacement placed = WindowPlacementRules.Restore(new WindowPlacement(2510, 200, 1180, 760), Both, MinW, MinH)!;
        placed.X.Should().Be((2560 - 1180) / 2);
    }

    [Fact]
    public void AWindowPartlyOffItsMonitorIsPulledBackOn()
    {
        // Reachable (its title bar is on the primary), but it hangs off the bottom right.
        WindowPlacement placed = WindowPlacementRules.Restore(new WindowPlacement(2000, 1000, 1180, 760), Both, MinW, MinH)!;
        placed.Should().Be(new WindowPlacement(2560 - 1180, 1400 - 760, 1180, 760));
    }

    [Fact]
    public void AWindowLargerThanItsMonitorIsShrunkToFit()
    {
        // Saved on a big monitor, now the 1920x1080 one (e.g. after a resolution change).
        WindowPlacement placed = WindowPlacementRules.Restore(new WindowPlacement(-1900, 0, 2500, 1300), Both, MinW, MinH)!;
        placed.Should().Be(new WindowPlacement(-1920, 0, 1920, 1040));
    }

    [Fact]
    public void ATooSmallWindowGrowsToTheMinimum()
    {
        WindowPlacement placed = WindowPlacementRules.Restore(new WindowPlacement(100, 100, 300, 200), Both, MinW, MinH)!;
        (placed.Width, placed.Height).Should().Be((MinW, MinH));
    }

    [Fact]
    public void ScalingIsRespected()
    {
        // 150%: a 1280x800 window is 1920x1200 pixels; it does not fit a 1600x1000 area at its position.
        var hiDpi = new ScreenArea(0, 0, 2400, 1500, 1.5, IsPrimary: true);
        WindowPlacement placed = WindowPlacementRules.Restore(new WindowPlacement(1000, 600, 1280, 800), [hiDpi], MinW, MinH)!;
        placed.Should().Be(new WindowPlacement(2400 - 1920, 1500 - 1200, 1280, 800));
    }

    [Fact]
    public void AMaximizedWindowStaysMaximizedOnItsMonitor()
    {
        WindowPlacement placed = WindowPlacementRules.Restore(new WindowPlacement(-1700, 100, 1200, 800, Maximized: true), Both, MinW, MinH)!;
        placed.Maximized.Should().BeTrue();
        placed.X.Should().BeNegative("it reopens on the left monitor");
    }

    [Fact]
    public void AMaximizedWindowOnAGoneMonitorIsMaximizedOnThePrimary()
    {
        WindowPlacement placed = WindowPlacementRules.Restore(new WindowPlacement(-1700, 100, 1200, 800, Maximized: true), [Primary], MinW, MinH)!;
        (placed.Maximized, Primary.Contains(placed.X, placed.Y)).Should().Be((true, true));
    }

    [Theory]
    [InlineData(0, 700)]
    [InlineData(-5, 700)]
    [InlineData(1000, double.NaN)]
    public void UnusableSavedSizesAreDropped(double width, double height) =>
        ConfigRules.Normalize(new AppConfig { ControlPanel = new WindowPlacement(1, 2, width, height) }).ControlPanel.Should().BeNull();

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ThePlacementAndItsSettingRoundTrip(bool remember)
    {
        AppConfig c = ConfigRules.Normalize(new AppConfig { RememberControlPanel = remember, ControlPanel = new WindowPlacement(-1700, 100, 1200, 800, true) });
        ConfigDocument.TryParse(ConfigDocument.Serialize(c), out AppConfig back, out _).Should().BeTrue();
        back.RememberControlPanel.Should().Be(remember);
        back.ControlPanel.Should().Be(new WindowPlacement(-1700, 100, 1200, 800, true));
    }

    [Fact]
    public void RememberingDefaultsToOn() => ConfigRules.Normalize(new AppConfig()).RememberControlPanel.Should().BeTrue();
}
