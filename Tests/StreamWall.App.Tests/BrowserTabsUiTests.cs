using Avalonia.Controls;
using StreamWall.App.ViewModels;
using StreamWall.App.Web;
using StreamWall.Core.Config;

namespace StreamWall.App.Tests;

/// <summary>Browser tabs, zoom per site, Open in Browser, and their settings (v0.2.0).</summary>
public sealed class BrowserTabsUiTests
{
    private const string A = "https://cams.example.invalid/a";
    private const string B = "https://cams.example.invalid/b";
    private const string C = "https://cams.example.invalid/c";

    private static AppConfig WithTabs(bool restore, params string[] tabs) =>
        new() { Browser = new BrowserConfig { Tabs = tabs, ActiveTab = tabs.Length - 1, RestoreTabs = restore } };

    // ---- Tabs ----

    [Fact]
    public Task AFreshStartHasOneTabAtTheStartPage() => TestApp.Run(() =>
    {
        using UiHarness ui = UiHarness.Start(new AppConfig());
        ui.ViewModel.Browser.Tabs.Should().ContainSingle().Which.Url.Should().Be(WebEnvironment.DemoHome);
        ui.ViewModel.Browser.SelectedTab.Should().BeSameAs(ui.ViewModel.Browser.Tabs[0]);
    });

    [Fact]
    public Task WithRestoreTabsEveryTabReopens() => TestApp.Run(() =>
    {
        using UiHarness ui = UiHarness.Start(WithTabs(true, A, B, C));
        ui.ViewModel.Browser.Tabs.Select(t => t.Url).Should().Equal(A, B, C);
        ui.ViewModel.Browser.SelectedTab!.Url.Should().Be(C);
    });

    [Fact]
    public Task WithoutRestoreTabsOneTabOpensAtTheLastPage() => TestApp.Run(() =>
    {
        using UiHarness ui = UiHarness.Start(WithTabs(false, A, B, C) with { Site = new SiteConfig { BrowserUrl = C } });
        ui.ViewModel.Browser.Tabs.Should().ContainSingle().Which.Url.Should().Be(C);
    });

    [Fact]
    public Task TabsFromLinksOpenInTheBackgroundByDefault() => TestApp.Run(() =>
    {
        using UiHarness ui = UiHarness.Start(WithTabs(true, A));
        BrowserViewModel browser = ui.ViewModel.Browser;

        BrowserTabViewModel opened = browser.OpenTab(B, browser.FocusNewTabs);

        browser.FocusNewTabs.Should().BeFalse();
        browser.SelectedTab!.Url.Should().Be(A, "the link's tab loads in the background");
        opened.LoadNow.Should().BeTrue();
        browser.Tabs.Select(t => t.Url).Should().Equal(A, B);
    });

    [Fact]
    public Task WithFocusNewTabsALinksTabComesToTheFront() => TestApp.Run(() =>
    {
        using UiHarness ui = UiHarness.Start(WithTabs(true, A) with { Browser = new BrowserConfig { Tabs = [A], FocusNewTabs = true } });
        BrowserViewModel browser = ui.ViewModel.Browser;
        browser.OpenTab(B, browser.FocusNewTabs);
        browser.SelectedTab!.Url.Should().Be(B);
    });

    [Fact]
    public Task ANewTabOpensRightAfterTheSelectedOne() => TestApp.Run(() =>
    {
        using UiHarness ui = UiHarness.Start(WithTabs(true, A, B) with { Browser = new BrowserConfig { Tabs = [A, B], ActiveTab = 0 } });
        ui.ViewModel.Browser.OpenTab(C, focus: false);
        ui.ViewModel.Browser.Tabs.Select(t => t.Url).Should().Equal(A, C, B);
    });

    [Fact]
    public Task ClosingTheSelectedTabSelectsItsNeighborAndTheListIsSaved() => TestApp.Run(() =>
    {
        using UiHarness ui = UiHarness.Start(WithTabs(true, A, B, C) with { Browser = new BrowserConfig { Tabs = [A, B, C], ActiveTab = 1 } });
        BrowserViewModel browser = ui.ViewModel.Browser;

        browser.CloseTabCommand.Execute(browser.Tabs[1]);

        browser.SelectedTab!.Url.Should().Be(C);
        ui.Settle();
        ui.Host.Config.Browser.Tabs.Should().Equal(A, C);
        ui.Host.Config.Browser.ActiveTab.Should().Be(1);
    });

    [Fact]
    public Task ClosingTheLastTabLeavesAFreshOne() => TestApp.Run(() =>
    {
        using UiHarness ui = UiHarness.Start(WithTabs(true, A));
        BrowserViewModel browser = ui.ViewModel.Browser;
        browser.CloseTabCommand.Execute(browser.Tabs[0]);
        browser.Tabs.Should().ContainSingle().Which.Url.Should().Be(WebEnvironment.DemoHome);
    });

    [Fact]
    public Task TheNewTabButtonOpensAndSelectsATab() => TestApp.Run(() =>
    {
        using UiHarness ui = UiHarness.Start(WithTabs(true, A));
        ui.Click(ui.Named<Button>("BrowserTabButton"));
        ui.Click(ui.Named<Button>("NewTabButton"));
        ui.ViewModel.Browser.Tabs.Should().HaveCount(2);
        ui.ViewModel.Browser.SelectedTab!.Url.Should().Be(WebEnvironment.DemoHome);
    });

    [Fact]
    public Task MiddleClickingATabClosesIt() => TestApp.Run(() =>
    {
        using UiHarness ui = UiHarness.Start(WithTabs(true, A, B));
        ui.Click(ui.Named<Button>("BrowserTabButton"));
        Border first = ui.FindAll<Border>(b => b.Name == "TabHeader").First();
        ui.MiddleClick(first);
        ui.ViewModel.Browser.Tabs.Select(t => t.Url).Should().Equal(B);
    });


    [Fact]
    public Task SendToWallUsesTheSelectedTab() => TestApp.Run(() =>
    {
        using UiHarness ui = UiHarness.Start(WithTabs(true, A, B) with { Browser = new BrowserConfig { Tabs = [A, B], ActiveTab = 0 } });
        Guid wall = ui.Host.Config.Walls.Count > 0 ? ui.Host.Config.Walls[0].Id : ui.Host.AddWall().Walls[0].Id;
        ui.ViewModel.PickCell = _ => Task.FromResult<CellRef?>(new CellRef(wall, 0));
        ui.ViewModel.Browser.SelectedTab = ui.ViewModel.Browser.Tabs[1];
        ui.ViewModel.Browser.Tabs[1].PageTitle = "Back yard";

        ui.ViewModel.Browser.SendToWallCommand.Execute(null);
        ui.WaitUntil(() => ui.Host.Config.Streams.Any(s => s.Url == B), "the stream");

        ui.Host.Config.Streams.Single(s => s.Url == B).Label.Should().Be("Back yard");
    });

    [Fact]
    public Task APageWithoutATitleIsNotLabeledNewTab() => TestApp.Run(() =>
    {
        using UiHarness ui = UiHarness.Start(WithTabs(true, A));
        ui.ViewModel.Browser.CurrentTitle.Should().BeNull();
        ui.ViewModel.Browser.SelectedTab!.Title.Should().Be("New tab", "the strip still shows something");
    });

    // ---- Zoom ----

    [Fact]
    public Task ZoomIsSavedPerSiteAndOfferedToItsPages() => TestApp.Run(() =>
    {
        using UiHarness ui = UiHarness.Start(WithTabs(true, A));
        ui.ViewModel.Browser.SaveZoom(A, 1.3);
        ui.ViewModel.Browser.ZoomFor(C).Should().Be(1.3);
        ui.ViewModel.Browser.ZoomFor("https://other.example.invalid/").Should().Be(1.0);
        ui.LogText.Should().NotContain("cams.example.invalid");
    });

    [Fact]
    public Task TheZoomButtonsStepLikeABrowser() => TestApp.Run(() =>
    {
        using UiHarness ui = UiHarness.Start(WithTabs(true, A));
        BrowserTabViewModel tab = ui.ViewModel.Browser.SelectedTab!;
        var requested = new List<double>();
        tab.ZoomRequested += requested.Add;

        ui.ViewModel.Browser.ZoomInCommand.Execute(null);
        tab.ZoomPercent = 125;
        ui.ViewModel.Browser.ZoomOutCommand.Execute(null);
        ui.ViewModel.Browser.ZoomResetCommand.Execute(null);

        requested.Should().Equal(1.1, 1.1, 1.0);
    });

    [Theory]
    [InlineData(100, 1, 1.1)]
    [InlineData(130, 1, 1.5)]
    [InlineData(130, -1, 1.25)]
    [InlineData(500, 1, 5.0)]
    [InlineData(25, -1, 0.25)]
    public void ZoomStepsRoundToTheNextBrowserLevel(int percent, int direction, double expected) =>
        BrowserViewModel.NextZoom(percent, direction).Should().Be(expected);

    // ---- Settings ----

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task RestoreTabsIsASetting(bool value) => TestApp.Run(() =>
    {
        using UiHarness ui = UiHarness.Start(new AppConfig { Browser = new BrowserConfig { RestoreTabs = !value } });
        ui.ViewModel.Settings.RestoreTabs = value;
        ui.Settle();
        ui.Host.Config.Browser.RestoreTabs.Should().Be(value);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task FocusNewTabsIsASetting(bool value) => TestApp.Run(() =>
    {
        using UiHarness ui = UiHarness.Start(new AppConfig { Browser = new BrowserConfig { FocusNewTabs = !value } });
        ui.ViewModel.Settings.FocusNewTabs = value;
        ui.Settle();
        ui.Host.Config.Browser.FocusNewTabs.Should().Be(value);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task RememberZoomIsASetting(bool value) => TestApp.Run(() =>
    {
        using UiHarness ui = UiHarness.Start(new AppConfig { Browser = new BrowserConfig { RememberZoom = !value } });
        ui.ViewModel.Settings.RememberZoom = value;
        ui.Settle();
        ui.Host.Config.Browser.RememberZoom.Should().Be(value);
    });

    [Fact]
    public Task ForgettingZoomClearsEverySite() => TestApp.Run(() =>
    {
        using UiHarness ui = UiHarness.Start(new AppConfig());
        ui.ViewModel.Browser.SaveZoom(A, 1.3);
        ui.ViewModel.Browser.SaveZoom("https://other.example.invalid/", 0.8);
        ui.Settle();
        ui.ViewModel.Settings.SavedZoomSites.Should().Be(2);

        ui.ViewModel.Settings.ForgetZoomCommand.Execute(null);

        ui.Host.Config.Browser.SiteZoom.Should().BeEmpty();
        ui.ViewModel.Settings.SavedZoomSites.Should().Be(0);
    });

    [Theory]
    [InlineData(0, OpenInBrowserMode.KeepOnWall)]
    [InlineData(1, OpenInBrowserMode.MoveOffWall)]
    [InlineData(2, OpenInBrowserMode.Ask)]
    public Task EveryOpenInBrowserChoiceIsSaved(int index, OpenInBrowserMode expected) => TestApp.Run(() =>
    {
        using UiHarness ui = UiHarness.Start(new AppConfig());
        ui.ViewModel.Settings.OpenInBrowser = SettingsPageViewModel.OpenInBrowserModes[index];
        ui.Settle();
        ui.Host.Config.OpenInBrowser.Should().Be(expected);
    });

    [Fact]
    public Task TheSettingsShowTheSavedBrowserOptions() => TestApp.Run(() =>
    {
        using UiHarness ui = UiHarness.Start(new AppConfig
        {
            OpenInBrowser = OpenInBrowserMode.Ask,
            Browser = new BrowserConfig { RestoreTabs = false, FocusNewTabs = true, RememberZoom = false },
        });
        SettingsPageViewModel s = ui.ViewModel.Settings;
        (s.OpenInBrowser.Mode, s.RestoreTabs, s.FocusNewTabs, s.RememberZoom).Should().Be((OpenInBrowserMode.Ask, false, true, false));
    });

    // ---- Open in Browser from a wall ----

    [Theory]
    [InlineData(OpenInBrowserMode.KeepOnWall, null, true, false)]
    [InlineData(OpenInBrowserMode.MoveOffWall, null, true, true)]
    [InlineData(OpenInBrowserMode.Ask, OpenInBrowserChoice.Open, true, false)]
    [InlineData(OpenInBrowserMode.Ask, OpenInBrowserChoice.Move, true, true)]
    [InlineData(OpenInBrowserMode.Ask, null, false, false)]
    public Task OpenInBrowserFollowsTheSetting(OpenInBrowserMode mode, OpenInBrowserChoice? answer, bool opens, bool clears) => TestApp.Run(() =>
    {
        using UiHarness ui = UiHarness.Start();
        ui.Host.Update(c => c with { OpenInBrowser = mode });
        bool asked = false;
        var wall = new WallViewModel(ui.Host, ui.Host.Config.Walls[0].Id, () => TimeSpan.Zero, null,
            _ => { asked = true; return Task.FromResult(answer); });
        wall.Apply(ui.Host.Config);
        ui.Host.ConfigChanged += wall.Apply;
        CellViewModel cell = wall.Cells[0];
        string url = cell.Url!;
        bool shown = false;
        ui.ViewModel.ShowRequested += () => shown = true;
        int tabsBefore = ui.ViewModel.Browser.Tabs.Count;

        cell.OpenInBrowserCommand.Execute(null);
        ui.Settle();

        asked.Should().Be(mode == OpenInBrowserMode.Ask);
        if (opens)
        {
            ui.ViewModel.Browser.Tabs.Should().HaveCount(tabsBefore + 1);
            ui.ViewModel.Browser.SelectedTab!.Url.Should().Be(url);
            ui.ViewModel.SelectedTab.Should().Be(ControlPanelTab.Browser);
            shown.Should().BeTrue("the control panel comes to the front");
        }
        else
        {
            ui.ViewModel.Browser.Tabs.Should().HaveCount(tabsBefore);
        }
        (ui.Host.Config.Walls[0].Cells[0] is null).Should().Be(clears);
        ui.Host.Config.Streams.Should().Contain(s => s.Url == url, "a moved stream stays in the library");
        ui.LogText.Should().NotContain("example.invalid");
    });
}
