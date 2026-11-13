using Avalonia.Controls;
using StreamWall.App.Services;
using StreamWall.App.ViewModels;
using StreamWall.App.Views;
using StreamWall.App.Web;
using StreamWall.Core.Config;
using StreamWall.Core.Presentation;

namespace StreamWall.App.Tests;

public sealed class MainWindowUiTests
{
    private static readonly string DemoPage = $"https://{WebEnvironment.DemoHost}/stream.html?n=5";

    [Fact]
    public Task TheDemoConfigLoadsIntoTheControlPanel() => TestApp.Run(() =>
    {
        using UiHarness ui = UiHarness.Start();
        ui.TextNamed("StatusText").Text.Should().Be("1 wall(s), 3 cell(s) showing, 3 stream(s) in the library.");
        ui.Window.Title.Should().Be("StreamWall (demo)");
        ui.ViewModel.Walls.Walls.Should().ContainSingle().Which.Name.Should().Be("Wall 1");
    });

    [Fact]
    public Task TabsSwitchPages() => TestApp.Run(() =>
    {
        using UiHarness ui = UiHarness.Start();
        ui.Click(ui.Named<Button>("StreamsTabButton"));
        ui.ViewModel.SelectedTab.Should().Be(ControlPanelTab.Streams);
        ui.Named<ItemsControl>("StreamList").IsEffectivelyVisible.Should().BeTrue();
        ui.Click(ui.Named<Button>("WallsTabButton"));
        ui.Named<ItemsControl>("WallList").IsEffectivelyVisible.Should().BeTrue();
    });

    [Fact]
    public Task WallsCanBeAddedUpToThree() => TestApp.Run(() =>
    {
        using UiHarness ui = UiHarness.Start();
        Button add = ui.Named<Button>("AddWallButton");
        ui.Click(add);
        ui.Click(add);
        ui.Host.Config.Walls.Select(w => w.Name).Should().Equal("Wall 1", "Wall 2", "Wall 3");
        add.IsEffectivelyEnabled.Should().BeFalse("three is the most");
    });

    [Fact]
    public Task AnEmptyConfigSaysSo() => TestApp.Run(() =>
    {
        using UiHarness ui = UiHarness.Start(new AppConfig());
        ui.TextNamed("StatusText").Text.Should().Be("0 wall(s), 0 cell(s) showing, 0 stream(s) in the library.");
        ui.ViewModel.Walls.HasNoWalls.Should().BeTrue();
    });

    [Fact]
    public Task APastedStreamIsMaskedUntilRevealed() => TestApp.Run(() =>
    {
        using UiHarness ui = UiHarness.Start(new AppConfig());
        ui.Click(ui.Named<Button>("StreamsTabButton"));
        ui.Type(ui.Named<TextBox>("PasteUrlBox"), "https://cams.example.invalid/view/7");
        ui.Click(ui.Named<Button>("AddStreamButton"));

        ui.Host.Config.Streams.Should().ContainSingle().Which.Label.Should().Be("Stream 1");
        TextBlock url = ui.Named<TextBlock>("StreamUrlText");
        url.Text.Should().Be("https://*****/****");
        ui.Click(ui.Named<Button>("RevealButton"));
        url.Text.Should().Be("https://cams.example.invalid/view/7");
    });

    [Fact]
    public Task AnAddressThatIsNotAUrlIsRefused() => TestApp.Run(() =>
    {
        using UiHarness ui = UiHarness.Start(new AppConfig());
        ui.Click(ui.Named<Button>("StreamsTabButton"));
        ui.Type(ui.Named<TextBox>("PasteUrlBox"), "not an address");
        ui.Click(ui.Named<Button>("AddStreamButton"));
        ui.Host.Config.Streams.Should().BeEmpty();
        ui.Named<TextBlock>("PasteError").Text.Should().Contain("https://");
    });

    [Fact]
    public Task SendToWallPutsTheBrowsersPageInThePickedCellAndLogsNoUrl() => TestApp.Run(() =>
    {
        using UiHarness ui = UiHarness.Start();
        Guid wall = ui.Host.Config.Walls[0].Id;
        ui.ViewModel.PickCell = _ => Task.FromResult<CellRef?>(new CellRef(wall, 3));
        ui.ViewModel.Browser.CurrentUrl = DemoPage;

        ui.ViewModel.Browser.SendToWallCommand.Execute(null);
        ui.WaitUntil(() => WallLayout.StreamAt(ui.Host.Config, new CellRef(wall, 3)) is not null, "the cell to fill");

        StreamConfig sent = ui.Host.Config.Streams.Single(s => s.Url == DemoPage);
        sent.Label.Should().Be("Stream 1");
        ui.ViewModel.Browser.Message.Should().Be("Sent to Wall 1, cell 4.");
        string log = ui.LogText;
        log.Should().Contain("sent to wall 1 cell 4").And.Contain(ui.Host.Log.Redactor.Pseudonyms.Stream(sent.Id));
        log.Should().NotContain("example.invalid").And.NotContain("stream.html");
    });

    [Fact]
    public Task CancelingThePickerChangesNothing() => TestApp.Run(() =>
    {
        using UiHarness ui = UiHarness.Start();
        AppConfig before = ui.Host.Config;
        ui.ViewModel.PickCell = _ => Task.FromResult<CellRef?>(null);
        ui.ViewModel.Browser.CurrentUrl = DemoPage;
        ui.ViewModel.Browser.SendToWallCommand.Execute(null);
        ui.Settle();
        ui.Host.Config.Should().BeSameAs(before);
    });

    [Fact]
    public Task ThePickerConfirmsBeforeReplacingAndOffersTheNextEmptyCell() => TestApp.Run(() =>
    {
        using UiHarness ui = UiHarness.Start();
        WallMap map = WallMap.From(ui.Host.Config);
        var picker = new PickerWindow(map, "this page", ui.Host.AddWall);
        Task<CellRef?> result = picker.ShowDialog<CellRef?>(ui.Window);
        ui.Settle();

        ui.FindIn<Button>(picker, b => b.Name == "NextEmptyButton").Content.Should().Be("Next empty cell (Wall 1, cell 4)");

        WallMapCell filled = map.Walls[0].Cells[0];
        picker.Choose(filled);
        ui.Settle();
        ui.FindIn<TextBlock>(picker, t => t.Text?.StartsWith("Replace", StringComparison.Ordinal) == true).Text
            .Should().Be("Replace \"Demo 1\" in Wall 1, cell 1?");
        ui.Click(ui.FindIn<Button>(picker, b => b.Name == "ReplaceButton"));

        ui.WaitUntil(() => result.IsCompleted, "the picker to close");
        result.Result.Should().Be(filled.Ref);
    });

    [Fact]
    public Task WithNoWallsThePickerCanAddOne() => TestApp.Run(() =>
    {
        using UiHarness ui = UiHarness.Start(new AppConfig());
        var picker = new PickerWindow(WallMap.From(ui.Host.Config), "this page", ui.Host.AddWall);
        Task<CellRef?> result = picker.ShowDialog<CellRef?>(ui.Window);
        ui.Settle();

        ui.Click(ui.FindIn<Button>(picker, b => b.Name == "AddWallButton"));
        ui.Click(ui.FindIn<Button>(picker, b => b.Name == "NextEmptyButton"));

        ui.WaitUntil(() => result.IsCompleted, "the picker to close");
        result.Result.Should().Be(new CellRef(ui.Host.Config.Walls.Single().Id, 0));
    });

    [Fact]
    public Task MarkingTheSignInPageSavesItAndItsPath() => TestApp.Run(() =>
    {
        using UiHarness ui = UiHarness.Start(new AppConfig());
        ui.ViewModel.Browser.CurrentUrl = "https://cams.example.invalid/account/login?next=1";
        ui.ViewModel.Browser.MarkSignInPageCommand.Execute(null);

        ui.Host.Config.Site.SignInUrl.Should().Be("https://cams.example.invalid/account/login?next=1");
        ui.Host.Config.Site.SignedOutUrlPattern.Should().Be("/account/login");
        ui.LogText.Should().Contain("sign-in page and signed-out pattern set").And.NotContain("account/login");
    });

    [Fact]
    public Task ClearingACellFromTheWallsTabKeepsTheStream() => TestApp.Run(() =>
    {
        using UiHarness ui = UiHarness.Start();
        ui.Click(ui.FindAll<Button>(b => b.Name == "ClearCell").First());
        ui.Host.Config.Walls[0].Cells[0].Should().BeNull();
        ui.Host.Config.Streams.Should().HaveCount(3);
        ui.LogText.Should().Contain("wall 1 cell 1 cleared");
    });

    [Fact]
    public Task StartingUpLogsNoUrlsOrLabels() => TestApp.Run(() =>
    {
        using UiHarness ui = UiHarness.Start();
        string log = ui.LogText;
        log.Should().Contain("config loaded; 3 stream(s)");
        log.Should().NotContain("example.invalid").And.NotContain("Demo 1").And.NotContain("http");
    });

    [Theory]
    [InlineData("https://cams.example.invalid/x", "https://cams.example.invalid/x")]
    [InlineData("cams.example.invalid/x", "https://cams.example.invalid/x")]
    [InlineData("about:blank", "about:blank")]
    [InlineData("two words", null)]
    [InlineData("localword", null)]
    public void TypedAddressesBecomeUrls(string typed, string? expected) => BrowserViewModel.ToAddress(typed).Should().Be(expected);
}
