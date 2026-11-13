using Avalonia.Controls;
using StreamWall.Core.Config;

namespace StreamWall.App.Tests;

public sealed class WallsTabUiTests
{
    [Fact]
    public Task TheMonitorListShowsTheConfiguredMonitor() => TestApp.Run(() =>
    {
        using UiHarness ui = UiHarness.Start();
        ui.Host.Update(c => WallLayout.UpdateWall(c, c.Walls[0].Id, w => w with { Monitor = 1 }));
        ui.Settle();

        ui.ViewModel.Walls.Monitors = ["Monitor 1: 1920 x 1080 (primary)", "Monitor 2: 2560 x 1440"];
        ui.Settle();

        ui.Named<ComboBox>("MonitorBox").SelectedIndex.Should().Be(1);
        ui.Host.Config.Walls[0].Monitor.Should().Be(1, "a new monitor list must not rewrite the choice");
    });

    [Fact]
    public Task ChangingTheGridResizesTheWall() => TestApp.Run(() =>
    {
        using UiHarness ui = UiHarness.Start();
        ui.ViewModel.Walls.Walls[0].Columns = 3;
        ui.Settle();
        WallConfig wall = ui.Host.Config.Walls[0];
        (wall.Rows, wall.Columns, wall.Cells.Count).Should().Be((2, 3, 6));
        wall.Cells[0].Should().NotBeNull("streams keep their row and column");
    });

    [Fact]
    public Task ClosingAWallFromItsSwitchIsSaved() => TestApp.Run(() =>
    {
        using UiHarness ui = UiHarness.Start();
        ui.Click(ui.Named<ToggleSwitch>("OpenSwitch"));
        ui.Host.Config.Walls[0].IsOpen.Should().BeFalse();
    });
}
