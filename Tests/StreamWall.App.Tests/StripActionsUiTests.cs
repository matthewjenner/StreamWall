using Avalonia.Controls;
using StreamWall.App.ViewModels;
using StreamWall.App.Views;
using StreamWall.App.Web;
using StreamWall.Core.Config;
using StreamWall.Core.Presentation;

namespace StreamWall.App.Tests;

/// <summary>Page-title labels, swap, reload, and the Stream labels setting (v0.1.4).</summary>
public sealed class StripActionsUiTests
{
    private static readonly string Page5 = $"https://{WebEnvironment.DemoHost}/stream.html?n=5";

    [Fact]
    public Task SendToWallLabelsTheStreamWithThePageTitle() => TestApp.Run(() =>
    {
        using UiHarness ui = UiHarness.Start();
        Guid wall = ui.Host.Config.Walls[0].Id;
        ui.ViewModel.PickCell = _ => Task.FromResult<CellRef?>(new CellRef(wall, 3));
        ui.ViewModel.Browser.CurrentUrl = Page5;
        ui.ViewModel.Browser.CurrentTitle = "  Demo stream 5 -  Live  ";

        ui.ViewModel.Browser.SendToWallCommand.Execute(null);
        ui.WaitUntil(() => ui.Host.Config.Streams.Any(s => s.Url == Page5), "the stream to be added");

        ui.Host.Config.Streams.Single(s => s.Url == Page5).Label.Should().Be("Demo stream 5 - Live");
        ui.LogText.Should().NotContain("Demo stream 5", "labels and titles are never logged");
    });

    [Theory]
    [InlineData(0, StreamLabelMode.PageTitle)]
    [InlineData(1, StreamLabelMode.PageTitleNewOnly)]
    [InlineData(2, StreamLabelMode.FollowPageTitle)]
    [InlineData(3, StreamLabelMode.Numbered)]
    public Task EveryStreamLabelsChoiceIsSaved(int index, StreamLabelMode expected) => TestApp.Run(() =>
    {
        using UiHarness ui = UiHarness.Start(new AppConfig());
        ui.ViewModel.Settings.LabelMode = SettingsPageViewModel.LabelModes[index];
        ui.Settle();
        ui.Host.Config.LabelMode.Should().Be(expected);
    });

    [Fact]
    public Task TheSettingsBoxShowsTheSavedLabelMode() => TestApp.Run(() =>
    {
        using UiHarness ui = UiHarness.Start(new AppConfig { LabelMode = StreamLabelMode.FollowPageTitle });
        ui.ViewModel.Settings.LabelMode.Mode.Should().Be(StreamLabelMode.FollowPageTitle);
    });

    [Fact]
    public Task NumberedModeIgnoresThePageTitle() => TestApp.Run(() =>
    {
        using UiHarness ui = UiHarness.Start();
        ui.Host.Update(c => c with { LabelMode = StreamLabelMode.Numbered });
        Guid wall = ui.Host.Config.Walls[0].Id;
        ui.ViewModel.PickCell = _ => Task.FromResult<CellRef?>(new CellRef(wall, 3));
        ui.ViewModel.Browser.CurrentUrl = Page5;
        ui.ViewModel.Browser.CurrentTitle = "Demo stream 5";

        ui.ViewModel.Browser.SendToWallCommand.Execute(null);
        ui.WaitUntil(() => ui.Host.Config.Streams.Any(s => s.Url == Page5), "the stream to be added");

        ui.Host.Config.Streams.Single(s => s.Url == Page5).Label.Should().Be("Stream 1");
    });

    [Fact]
    public Task RenamingAStreamMarksTheLabelAsTheUsers() => TestApp.Run(() =>
    {
        using UiHarness ui = UiHarness.Start();
        StreamRowViewModel row = ui.ViewModel.Streams.Streams[0];
        row.Label = "  My name  ";
        ui.Settle();
        StreamConfig stream = ui.Host.Config.Streams.Single(s => s.Id == row.Id);
        (stream.Label, stream.LabelEdited).Should().Be(("My name", true));
    });

    [Fact]
    public Task ACellsPageTitleFillsADefaultLabel() => TestApp.Run(() =>
    {
        using UiHarness ui = UiHarness.Start();
        WallViewModel wall = OpenWallModel(ui);
        Guid id = wall.Cells[0].StreamId!.Value;
        ui.Host.Update(c => WallLayout.UpdateStream(c, id, s => s with { Label = "Stream 4" }));
        wall.Apply(ui.Host.Config);

        wall.Cells[0].ReportPageTitle("Demo stream 1 - Live");

        ui.Host.Config.Streams.Single(s => s.Id == id).Label.Should().Be("Demo stream 1 - Live");
    });

    [Fact]
    public Task TheSwapButtonSwapsWithThePickedCellAndLogsIt() => TestApp.Run(() =>
    {
        using UiHarness ui = UiHarness.Start();
        Guid wallId = ui.Host.Config.Walls[0].Id;
        Guid? first = ui.Host.Config.Walls[0].Cells[0];
        Guid? third = ui.Host.Config.Walls[0].Cells[2];
        WallViewModel wall = OpenWallModel(ui, _ => Task.FromResult<CellRef?>(new CellRef(wallId, 2)));

        wall.Cells[0].SwapCommand.Execute(null);
        ui.WaitUntil(() => ui.Host.Config.Walls[0].Cells[0] == third, "the swap");

        ui.Host.Config.Walls[0].Cells[2].Should().Be(first);
        ui.LogText.Should().Contain("wall 1 cell 1 swapped with wall 1 cell 3");
    });

    [Fact]
    public Task CancelingTheSwapPickerChangesNothing() => TestApp.Run(() =>
    {
        using UiHarness ui = UiHarness.Start();
        IReadOnlyList<Guid?> before = ui.Host.Config.Walls[0].Cells;
        WallViewModel wall = OpenWallModel(ui, _ => Task.FromResult<CellRef?>(null));
        wall.Cells[0].SwapCommand.Execute(null);
        ui.Settle();
        ui.Host.Config.Walls[0].Cells.Should().Equal(before);
    });

    [Fact]
    public Task TheSwapPickerMarksTheCellAndPicksWithoutAConfirm() => TestApp.Run(() =>
    {
        using UiHarness ui = UiHarness.Start();
        WallMap map = WallMap.From(ui.Host.Config);
        WallMapCell self = map.Walls[0].Cells[0];
        WallMapCell other = map.Walls[0].Cells[1];
        var picker = new PickerWindow(map, "\"Demo 1\"", ui.Host.AddWall, self.Ref);
        Task<CellRef?> result = picker.ShowDialog<CellRef?>(ui.Window);
        ui.Settle();

        ui.FindIn<TextBlock>(picker, t => t.Text == "Swap \"Demo 1\" with:", "the swap heading").Should().NotBeNull();
        ui.FindIn<Button>(picker, b => b.Name == "NextEmptyButton").Content.Should().Be("Move to next empty cell (Wall 1, cell 4)");

        picker.Choose(self);
        ui.Settle();
        result.IsCompleted.Should().BeFalse("a cell cannot be swapped with itself");

        picker.Choose(other);
        ui.WaitUntil(() => result.IsCompleted, "the picker to close");
        result.Result.Should().Be(other.Ref, "a filled cell is chosen at once: a swap loses nothing");
    });

    [Fact]
    public Task ReloadAsksTheCellToReloadNowAndLogsIt() => TestApp.Run(() =>
    {
        using UiHarness ui = UiHarness.Start();
        WallViewModel wall = OpenWallModel(ui);
        var delays = new List<TimeSpan>();
        wall.Cells[1].ReloadRequested += delays.Add;

        wall.Cells[1].ReloadCommand.Execute(null);

        delays.Should().Equal(TimeSpan.Zero);
        ui.LogText.Should().Contain("wall 1 cell 2 reloaded by the user");
    });

    [Fact]
    public Task ReloadAllReloadsEveryFilledCellStaggered() => TestApp.Run(() =>
    {
        using UiHarness ui = UiHarness.Start();
        var delay = TimeSpan.Zero;
        WallViewModel wall = OpenWallModel(ui, nextDelay: () => delay += TimeSpan.FromMilliseconds(300));
        var requests = new List<TimeSpan>();
        foreach (CellViewModel cell in wall.Cells)
            cell.ReloadRequested += requests.Add;

        wall.ReloadAll().Should().Be(3, "the demo wall has three streams and one empty cell");
        requests.Should().OnlyHaveUniqueItems("each cell starts at its own time");
    });

    [Fact]
    public Task TheReloadAllButtonAsksTheWalls() => TestApp.Run(() =>
    {
        using UiHarness ui = UiHarness.Start();
        bool asked = false;
        ui.Host.ReloadAllRequested += () => asked = true;
        ui.Click(ui.Named<Button>("ReloadAllButton"));
        asked.Should().BeTrue();
    });

    private static WallViewModel OpenWallModel(UiHarness ui, Func<CellViewModel, Task<CellRef?>>? pick = null, Func<TimeSpan>? nextDelay = null)
    {
        var wall = new WallViewModel(ui.Host, ui.Host.Config.Walls[0].Id, nextDelay ?? (() => TimeSpan.Zero), pick);
        wall.Apply(ui.Host.Config);
        ui.Host.ConfigChanged += wall.Apply;
        return wall;
    }
}
