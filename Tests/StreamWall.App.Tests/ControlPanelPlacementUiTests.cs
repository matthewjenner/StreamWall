using Avalonia;
using Avalonia.Controls;
using StreamWall.Core.Config;

namespace StreamWall.App.Tests;

/// <summary>The control panel reopens where it was: saving and restoring, with the safety cases.</summary>
public sealed class ControlPanelPlacementUiTests
{
    [Fact]
    public Task MovingAndResizingIsSaved() => TestApp.Run(() =>
    {
        using UiHarness ui = UiHarness.Start(new AppConfig());
        ui.Window.Position = new PixelPoint(140, 90);
        ui.Window.Width = 1000;
        ui.Window.Height = 640;
        ui.WaitUntil(() => ui.Host.Config.ControlPanel is { X: 140, Y: 90, Width: 1000, Height: 640 }, "the placement to be saved", 5000);
        ui.Host.Config.ControlPanel!.Maximized.Should().BeFalse();
    });

    [Fact]
    public Task MinimizingNeverOverwritesTheSavedPlacement() => TestApp.Run(() =>
    {
        using UiHarness ui = UiHarness.Start(new AppConfig());
        ui.Window.Position = new PixelPoint(140, 90);
        ui.WaitUntil(() => ui.Host.Config.ControlPanel is { X: 140 }, "the placement to be saved", 5000);
        WindowPlacement before = ui.Host.Config.ControlPanel!;

        ui.Window.WindowState = WindowState.Minimized;
        ui.Settle(1000);
        ui.Window.SavePlacement(); // as on close

        ui.Host.Config.ControlPanel.Should().Be(before);
    });

    [Fact]
    public Task MaximizingKeepsTheLastNormalBounds() => TestApp.Run(() =>
    {
        using UiHarness ui = UiHarness.Start(new AppConfig());
        ui.Window.Position = new PixelPoint(140, 90);
        ui.Window.Width = 1000;
        ui.WaitUntil(() => ui.Host.Config.ControlPanel is { X: 140, Width: 1000 }, "the placement to be saved", 5000);

        ui.Window.WindowState = WindowState.Maximized;
        ui.WaitUntil(() => ui.Host.Config.ControlPanel is { Maximized: true }, "maximized to be saved", 5000);

        ui.Host.Config.ControlPanel.Should().Be(new WindowPlacement(140, 90, 1000, ui.Host.Config.ControlPanel!.Height, Maximized: true));
    });

    [Fact]
    public Task WithTheSettingOffNothingIsSavedOrRestored() => TestApp.Run(() =>
    {
        var saved = new WindowPlacement(140, 90, 1000, 640);
        using UiHarness ui = UiHarness.Start(new AppConfig { RememberControlPanel = false, ControlPanel = saved });
        ui.Window.RestorePlacement();
        ui.Window.Position.Should().NotBe(new PixelPoint(140, 90));

        ui.Window.Position = new PixelPoint(300, 200);
        ui.Settle(1000);
        ui.Window.SavePlacement();
        ui.Host.Config.ControlPanel.Should().Be(saved, "nothing is written while the setting is off");
    });

    [Fact]
    public Task ASavedPlacementIsRestored() => TestApp.Run(() =>
    {
        using UiHarness ui = UiHarness.Start(new AppConfig { ControlPanel = new WindowPlacement(120, 80, 980, 620) });
        Assert.SkipWhen(ui.Window.Screens.All.Count == 0, "no monitors on this headless platform (covered in Core)");
        ui.Window.RestorePlacement();
        ui.Settle();
        (ui.Window.Position, ui.Window.Width, ui.Window.Height).Should().Be((new PixelPoint(120, 80), 980d, 620d));
    });

    [Fact]
    public Task AnOffScreenPlacementIsBroughtBack() => TestApp.Run(() =>
    {
        using UiHarness ui = UiHarness.Start(new AppConfig { ControlPanel = new WindowPlacement(-30000, -30000, 980, 620) });
        Assert.SkipWhen(ui.Window.Screens.All.Count == 0, "no monitors on this headless platform (covered in Core)");
        ui.Window.RestorePlacement();
        ui.Settle();
        ui.Window.Screens.All.Any(s => s.WorkingArea.Contains(ui.Window.Position)).Should().BeTrue("it is recentered on a monitor");
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task RememberControlPanelIsASetting(bool value) => TestApp.Run(() =>
    {
        using UiHarness ui = UiHarness.Start(new AppConfig { RememberControlPanel = !value });
        ui.ViewModel.Settings.RememberControlPanel.Should().Be(!value);
        ui.ViewModel.Settings.RememberControlPanel = value;
        ui.Settle();
        ui.Host.Config.RememberControlPanel.Should().Be(value);
    });
}
