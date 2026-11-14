using Avalonia;
using Avalonia.Controls;
using StreamWall.App.Services;
using StreamWall.App.Views;
using StreamWall.Core.Config;

namespace StreamWall.App.Tests;

/// <summary>Wall windows: a windowed wall is a normal window that remembers where it was put.</summary>
public sealed class WallWindowUiTests
{
    private static AppConfig OneWall(bool fullscreen, WallBounds? bounds = null) =>
        new() { Walls = [new WallConfig { Name = "Wall 1", Rows = 1, Columns = 1, Fullscreen = fullscreen, Bounds = bounds }] };

    [Fact]
    public Task AWindowedWallHasATitleBarAndResizes() => TestApp.Run(() =>
    {
        using UiHarness ui = UiHarness.Start(OneWall(fullscreen: false));
        var walls = new WallManager(ui.Host);
        ui.WaitUntil(() => walls.OpenWindows.Count == 1, "the wall to open");
        WallWindow wall = walls.OpenWindows.Single();

        wall.WindowDecorations.Should().Be(WindowDecorations.Full);
        wall.CanResize.Should().BeTrue();
        wall.ShowActivated.Should().BeFalse("walls must not take focus from the control panel");
        walls.CloseAll();
    });

    [Fact]
    public Task AFullscreenWallIsBorderless() => TestApp.Run(() =>
    {
        using UiHarness ui = UiHarness.Start(OneWall(fullscreen: true));
        var walls = new WallManager(ui.Host);
        ui.WaitUntil(() => walls.OpenWindows.Count == 1, "the wall to open");
        walls.OpenWindows.Single().WindowDecorations.Should().Be(WindowDecorations.None);
        walls.CloseAll();
    });

    [Fact]
    public Task MovingAndResizingAWindowedWallIsRemembered() => TestApp.Run(() =>
    {
        using UiHarness ui = UiHarness.Start(OneWall(fullscreen: false));
        var walls = new WallManager(ui.Host);
        ui.WaitUntil(() => walls.OpenWindows.Count == 1, "the wall to open");
        WallWindow wall = walls.OpenWindows.Single();
        ui.Settle(300);

        int moved = 0, sized = 0;
        wall.PositionChanged += (_, _) => moved++;
        wall.SizeChanged += (_, _) => sized++;
        wall.Position = new PixelPoint(120, 90);
        wall.Width = 700;
        wall.Height = 420;
        ui.Settle(1500);
        ui.Host.Config.Walls[0].Bounds.Should().NotBeNull(
            $"moved={moved} sized={sized} state={wall.WindowState} visible={wall.IsVisible} client={wall.ClientSize} pos={wall.Position}");

        WallBounds saved = ui.Host.Config.Walls[0].Bounds!;
        (saved.X, saved.Y, saved.Width, saved.Height).Should().Be((120, 90, 700d, 420d));
        walls.CloseAll();
    });

    [Fact]
    public Task ChangingAWallsMonitorForgetsItsSavedPosition() => TestApp.Run(() =>
    {
        using UiHarness ui = UiHarness.Start(OneWall(fullscreen: false, new WallBounds(120, 90, 700, 420)));
        ui.ViewModel.Walls.Monitors = ["Monitor 1", "Monitor 2"];
        ui.Settle();
        ui.ViewModel.Walls.Walls[0].Monitor = 1;
        ui.Settle();
        ui.Host.Config.Walls[0].Monitor.Should().Be(1);
        ui.Host.Config.Walls[0].Bounds.Should().BeNull("the wall recenters on its new monitor");
    });

    [Fact]
    public Task TheNewStreamVolumeIsASetting() => TestApp.Run(() =>
    {
        using UiHarness ui = UiHarness.Start(new AppConfig());
        ui.ViewModel.Settings.NewStreamVolume.Should().Be(1);
        ui.ViewModel.Settings.NewStreamVolume = 30;
        ui.Settle();
        ui.Host.Config.NewStreamVolume.Should().Be(30);
    });
}
