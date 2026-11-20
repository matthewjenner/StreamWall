using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using StreamWall.App.ViewModels;
using StreamWall.Core.Config;

namespace StreamWall.App.Tests;

/// <summary>v0.2.1: steady fixed-width tabs, a hidden scroll bar, the on-top pin, the update check.</summary>
public sealed class TabStripAndControlPanelUiTests
{
    private static AppConfig ManyTabs(int count, int width = 200) => new()
    {
        Browser = new BrowserConfig
        {
            Tabs = [.. Enumerable.Range(0, count).Select(i => $"https://cams.example.invalid/{i}")],
            TabWidth = width,
        },
    };

    private static UiHarness OnBrowser(AppConfig config)
    {
        UiHarness ui = UiHarness.Start(config);
        ui.Click(ui.Named<Button>("BrowserTabButton"));
        return ui;
    }

    private static List<Border> Headers(UiHarness ui) => [.. ui.FindAll<Border>(b => b.Name == "TabHeader")];

    // ---- Steady tabs ----

    [Fact]
    public Task EveryTabHasTheSameFixedWidth() => TestApp.Run(() =>
    {
        using UiHarness ui = OnBrowser(ManyTabs(3, width: 180));
        ui.ViewModel.Browser.Tabs[0].PageTitle = "A";
        ui.ViewModel.Browser.Tabs[1].PageTitle = "A much, much longer page title that would push its neighbors around";
        ui.Settle();
        Headers(ui).Select(h => h.Bounds.Width).Should().AllBeEquivalentTo(180d);
    });

    [Fact]
    public Task TitleAndAudioChangesNeverMoveTabs() => TestApp.Run(() =>
    {
        using UiHarness ui = OnBrowser(ManyTabs(3));
        BrowserTabViewModel first = ui.ViewModel.Browser.Tabs[0];
        Border third = Headers(ui)[2];
        TextBlock firstTitle = Headers(ui)[0].GetVisualDescendants().OfType<TextBlock>().First();
        ui.Settle();
        Point before = third.TranslatePoint(default, ui.Window)!.Value;
        Point titleBefore = firstTitle.TranslatePoint(default, ui.Window)!.Value;

        for (int i = 0; i < 5; i++)
        {
            first.PageTitle = new string('x', i * 30) + " (" + i + ") LIVE";
            first.IsPlayingAudio = i % 2 == 0;
            ui.Settle(30);
        }

        third.TranslatePoint(default, ui.Window)!.Value.Should().Be(before, "neighbors stay put");
        firstTitle.TranslatePoint(default, ui.Window)!.Value.Should().Be(titleBefore, "the speaker's slot is always reserved");
    });

    [Fact]
    public Task TheSpeakerAppearsOnlyWhilePlayingOrMutedButKeepsItsPlace() => TestApp.Run(() =>
    {
        using UiHarness ui = OnBrowser(ManyTabs(1));
        BrowserTabViewModel tab = ui.ViewModel.Browser.Tabs[0];
        Button audio = ui.FindAll<Button>(b => b.Name == "TabAudioButton").Single();
        (audio.Opacity, audio.IsHitTestVisible).Should().Be((0d, false));

        tab.IsPlayingAudio = true;
        ui.Settle();
        (audio.Opacity, audio.IsHitTestVisible).Should().Be((1d, true));
        ui.Click(audio);
        tab.IsMuted.Should().BeTrue("the tab's speaker mutes it");
    });

    [Fact]
    public Task TitlesUpdateInPlaceWithoutRebuildingTheStrip() => TestApp.Run(() =>
    {
        using UiHarness ui = OnBrowser(ManyTabs(2));
        Border header = Headers(ui)[0];
        ui.ViewModel.Browser.Tabs[0].PageTitle = "Front door";
        ui.Settle();
        Headers(ui)[0].Should().BeSameAs(header);
        header.GetVisualDescendants().OfType<TextBlock>().Should().Contain(t => t.Text == "Front door");
    });

    [Fact]
    public Task TheTabStripNeverShowsAScrollBarAndTheWheelScrollsIt() => TestApp.Run(() =>
    {
        using UiHarness ui = OnBrowser(ManyTabs(20));
        var scroller = ui.Named<ScrollViewer>("TabStripScroller");
        scroller.HorizontalScrollBarVisibility.Should().Be(ScrollBarVisibility.Hidden);
        scroller.Extent.Width.Should().BeGreaterThan(scroller.Viewport.Width, "twenty tabs overflow");
        ui.FindAll<ScrollBar>(s => s.Orientation == Avalonia.Layout.Orientation.Horizontal && s.IsVisible
            && scroller.IsVisualAncestorOf(s)).Should().BeEmpty();

        scroller.Offset = default;
        Point at = scroller.TranslatePoint(new Point(scroller.Bounds.Width / 2, scroller.Bounds.Height / 2), ui.Window)!.Value;
        ui.Window.MouseWheel(at, new Vector(0, -3), RawInputModifiers.None);
        ui.Settle();
        scroller.Offset.X.Should().BeGreaterThan(0, "the wheel scrolls the tabs sideways");
    });

    [Fact]
    public Task TheSelectedTabIsScrolledIntoView() => TestApp.Run(() =>
    {
        using UiHarness ui = OnBrowser(ManyTabs(20));
        var scroller = ui.Named<ScrollViewer>("TabStripScroller");
        scroller.Offset = default;
        ui.ViewModel.Browser.SelectedTab = ui.ViewModel.Browser.Tabs[19];
        ui.Settle();
        scroller.Offset.X.Should().BeGreaterThan(0);
    });

    [Theory]
    [InlineData(150)]
    [InlineData(300)]
    public Task TabWidthIsASettingAndAppliesAtOnce(int width) => TestApp.Run(() =>
    {
        using UiHarness ui = OnBrowser(ManyTabs(2));
        ui.ViewModel.Settings.TabWidth = width;
        ui.Settle();
        ui.Host.Config.Browser.TabWidth.Should().Be(width);
        Headers(ui).Select(h => h.Bounds.Width).Should().AllBeEquivalentTo((double)width);
    });

    // ---- On top ----

    [Fact]
    public Task ThePinKeepsTheControlPanelOnTopAndIsRemembered() => TestApp.Run(() =>
    {
        using UiHarness ui = UiHarness.Start(new AppConfig());
        ui.Window.Topmost.Should().BeFalse();

        ui.Click(ui.Named<ToggleButton>("PinButton"));
        ui.Window.Topmost.Should().BeTrue();
        ui.Host.Config.ControlPanelOnTop.Should().BeTrue();
        ui.ViewModel.Settings.ControlPanelOnTop.Should().BeTrue("Settings stays in step with the pin");

        ui.Click(ui.Named<ToggleButton>("PinButton"));
        (ui.Window.Topmost, ui.Host.Config.ControlPanelOnTop).Should().Be((false, false));
    });

    [Fact]
    public Task OnTopFromSettingsMovesThePinToo() => TestApp.Run(() =>
    {
        using UiHarness ui = UiHarness.Start(new AppConfig());
        ui.ViewModel.Settings.ControlPanelOnTop = true;
        ui.Settle();
        (ui.Window.Topmost, ui.Named<ToggleButton>("PinButton").IsChecked).Should().Be((true, (bool?)true));
    });

    [Fact]
    public Task ASavedOnTopIsAppliedOnLaunch() => TestApp.Run(() =>
    {
        using UiHarness ui = UiHarness.Start(new AppConfig { ControlPanelOnTop = true });
        ui.Window.Topmost.Should().BeTrue();
    });

    // ---- Updates ----

    [Fact]
    public Task TheSettingsShowTheVersionAndThatDemoCopiesDoNotUpdate() => TestApp.Run(() =>
    {
        using UiHarness ui = UiHarness.Start(new AppConfig());
        ui.Click(ui.Named<Button>("SettingsTabButton"));
        ui.Named<TextBlock>("VersionText").Text.Should().StartWith("StreamWall v").And.EndWith("(demo)");
        ui.Named<TextBlock>("UpdateStatusText").Text.Should().Contain("off in dev and demo");
        ui.Named<Button>("CheckForUpdatesButton").IsEffectivelyEnabled.Should().BeFalse();
    });
}
