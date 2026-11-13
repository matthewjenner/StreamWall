using StreamWall.App.Services;
using StreamWall.App.ViewModels;
using StreamWall.App.Views;
using StreamWall.Core.Config;

namespace StreamWall.App.Tests;

/// <summary>Quitting: walls stay marked open, and the last change is saved (both broke in the first real run).</summary>
public sealed class ShutdownUiTests
{
    [Fact]
    public Task ClosingTheControlPanelKeepsWallsOpenAndSavesTheLastChange() => TestApp.Run(() =>
    {
        // Walls with no streams: headless has no WebView2, and empty cells create none.
        var seed = new AppConfig { Walls = [new WallConfig { Name = "Wall 1" }, new WallConfig { Name = "Wall 2", Rows = 1, Columns = 1 }] };
        using UiHarness ui = UiHarness.Start(seed);
        var walls = new WallManager(ui.Host);
        ui.WaitUntil(() => walls.OpenWindows.Count == 2, "both walls to open");
        Shutdown.Wire(ui.Window, walls, ui.Host);

        ui.Host.Update(c => c with { MasterVolume = 37 });   // pending: saved 400 ms later, unless we quit first
        bool closed = false;
        ui.Window.Closed += (_, _) => closed = true;
        ui.Window.Close();
        ui.WaitUntil(() => closed, "the control panel to close");

        walls.OpenWindows.Should().BeEmpty();
        string json = ((MemoryConfigStore)ui.Host.Store).Json!;
        ConfigDocument.TryParse(json, out AppConfig saved, out _).Should().BeTrue();
        saved.MasterVolume.Should().Be(37, "the pending change is flushed before the window closes");
        saved.Walls.Should().HaveCount(2).And.AllSatisfy(w => w.IsOpen.Should().BeTrue("quitting is not closing a wall"));
        ui.LogText.Should().NotContain("closed", "no wall was closed by the user");
    });

    [Fact]
    public Task ClosingAWallWindowYourselfMarksItClosed() => TestApp.Run(() =>
    {
        var seed = new AppConfig { Walls = [new WallConfig { Name = "Wall 1", Rows = 1, Columns = 1 }] };
        using UiHarness ui = UiHarness.Start(seed);
        var walls = new WallManager(ui.Host);
        ui.WaitUntil(() => walls.OpenWindows.Count == 1, "the wall to open");

        walls.OpenWindows.Single().Close();
        ui.Settle();

        ui.Host.Config.Walls[0].IsOpen.Should().BeFalse();
        ui.LogText.Should().Contain("wall 1 closed");
    });
}
