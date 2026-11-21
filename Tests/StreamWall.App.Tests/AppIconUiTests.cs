using StreamWall.App.Services;
using StreamWall.App.Views;
using StreamWall.Core.Config;
using StreamWall.Core.Presentation;

namespace StreamWall.App.Tests;

/// <summary>Every window shows the app icon (title bar, taskbar, Alt+Tab), not the default.</summary>
public sealed class AppIconUiTests
{
    [Fact]
    public Task TheIconLoadsFromTheAppsResources() => TestApp.Run(() =>
        AppIcon.Window.Should().NotBeNull("Assets/icon.ico is embedded as an Avalonia resource"));

    [Fact]
    public Task EveryWindowHasTheAppIcon() => TestApp.Run(() =>
    {
        using UiHarness ui = UiHarness.Start(new AppConfig { Walls = [new WallConfig { Name = "Wall 1", Rows = 1, Columns = 1 }] });
        ui.Window.Icon.Should().BeSameAs(AppIcon.Window, "the control panel");

        var walls = new WallManager(ui.Host);
        ui.WaitUntil(() => walls.OpenWindows.Count == 1, "the wall to open");
        walls.OpenWindows.Single().Icon.Should().BeSameAs(AppIcon.Window, "a wall");
        walls.CloseAll();

        new PickerWindow(WallMap.From(ui.Host.Config), "x", ui.Host.AddWall).Icon.Should().BeSameAs(AppIcon.Window, "the picker");
        new OpenInBrowserDialog("x").Icon.Should().BeSameAs(AppIcon.Window, "the Open in Browser dialog");
    });
}
