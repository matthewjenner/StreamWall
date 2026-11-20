using StreamWall.Core.Config;
using StreamWall.Core.Diagnostics;

namespace StreamWall.Core.Tests;

/// <summary>Browser tabs, zoom per site, and every browser setting (v0.2.0).</summary>
public sealed class BrowserRulesTests
{
    private const string A = "https://cams.example.invalid/a";
    private const string B = "https://cams.example.invalid/b";
    private const string Other = "https://other.example.invalid/x";

    private static AppConfig Fresh(BrowserConfig? browser = null) =>
        ConfigRules.Normalize(new AppConfig { Browser = browser ?? new BrowserConfig() });

    // ---- Tabs ----

    [Fact]
    public void TabsAreKeptInOrderOnlyHttpPagesAndCapped()
    {
        string[] many = [.. Enumerable.Range(0, 40).Select(i => $"https://cams.example.invalid/{i}")];
        AppConfig c = Fresh(new BrowserConfig { Tabs = ["about:blank", A, "  " + B + " ", "not a url", .. many], ActiveTab = 99 });
        c.Browser.Tabs.Should().HaveCount(BrowserRules.MaxTabs);
        c.Browser.Tabs.Take(2).Should().Equal(A, B);
        c.Browser.ActiveTab.Should().Be(BrowserRules.MaxTabs - 1, "an out-of-range selection is clamped");
    }

    [Fact]
    public void AnOlderConfigsLastPageBecomesTheFirstTab()
    {
        ConfigDocument.TryParse("{\"site\":{\"browserUrl\":\"" + A + "\"}}", out AppConfig c, out _).Should().BeTrue();
        c.Browser.Tabs.Should().Equal(A);
    }

    [Fact]
    public void WithRestoreTabsEveryTabReopens()
    {
        AppConfig c = BrowserRules.WithTabs(Fresh(), [A, B], 1);
        (IReadOnlyList<string> tabs, int active) = BrowserRules.StartTabs(c);
        tabs.Should().Equal(A, B);
        active.Should().Be(1);
    }

    [Fact]
    public void WithoutRestoreTabsOneTabOpensAtTheLastPage()
    {
        AppConfig c = BrowserRules.WithTabs(Fresh(new BrowserConfig { RestoreTabs = false }), [A, B], 1);
        (IReadOnlyList<string> tabs, int active) = BrowserRules.StartTabs(c);
        tabs.Should().Equal(B);
        active.Should().Be(0);
    }

    [Fact]
    public void NoTabsAndNoLastPageOpensNothingSoTheAppUsesItsStartPage() =>
        BrowserRules.StartTabs(Fresh()).Tabs.Should().BeEmpty();

    [Fact]
    public void SavingTabsSkipsPagesThatCannotBeRestoredAndKeepsTheSelection()
    {
        AppConfig c = BrowserRules.WithTabs(Fresh(), [A, "about:blank", B], 2);
        c.Browser.Tabs.Should().Equal(A, B);
        c.Browser.ActiveTab.Should().Be(1);
        c.Site.BrowserUrl.Should().Be(B, "the selected tab is the last page");
    }

    [Fact]
    public void SavingTheSameTabsChangesNothing()
    {
        AppConfig c = BrowserRules.WithTabs(Fresh(), [A, B], 0);
        BrowserRules.WithTabs(c, [A, B], 0).Should().BeSameAs(c);
    }

    // ---- Zoom per site ----

    [Fact]
    public void ZoomIsRememberedPerSiteAcrossItsPages()
    {
        AppConfig c = BrowserRules.WithZoom(Fresh(), A, 1.3);
        BrowserRules.ZoomFor(c, B).Should().Be(1.3, "same site, another page");
        BrowserRules.ZoomFor(c, "https://WWW.CAMS.example.invalid/c").Should().Be(1.3, "case and www do not matter");
        BrowserRules.ZoomFor(c, Other).Should().Be(1.0, "another site is unzoomed");
    }

    [Fact]
    public void BackTo100PercentForgetsTheSite()
    {
        AppConfig c = BrowserRules.WithZoom(BrowserRules.WithZoom(Fresh(), A, 1.5), A, 1.0);
        c.Browser.SiteZoom.Should().BeEmpty();
    }

    [Fact]
    public void ZoomIsClampedAndRounded()
    {
        BrowserRules.ZoomFor(BrowserRules.WithZoom(Fresh(), A, 9), A).Should().Be(BrowserRules.MaxZoom);
        BrowserRules.ZoomFor(BrowserRules.WithZoom(Fresh(), A, 0.01), A).Should().Be(BrowserRules.MinZoom);
        BrowserRules.ZoomFor(BrowserRules.WithZoom(Fresh(), A, 1.3333333), A).Should().Be(1.33);
    }

    [Fact]
    public void WithRememberZoomOffNothingIsSavedOrApplied()
    {
        AppConfig off = Fresh(new BrowserConfig { RememberZoom = false, SiteZoom = new Dictionary<string, double> { ["cams.example.invalid"] = 1.5 } });
        BrowserRules.WithZoom(off, A, 1.3).Should().BeSameAs(off);
        BrowserRules.ZoomFor(off, A).Should().BeNull("the page's own zoom is left alone");
    }

    [Fact]
    public void UnchangedOrUnusableZoomChangesNothing()
    {
        AppConfig c = BrowserRules.WithZoom(Fresh(), A, 1.3);
        BrowserRules.WithZoom(c, B, 1.3).Should().BeSameAs(c);
        BrowserRules.WithZoom(c, "about:blank", 2).Should().BeSameAs(c);
        BrowserRules.WithZoom(c, A, double.NaN).Should().BeSameAs(c);
        BrowserRules.WithZoom(Fresh(), A, 1.0).Should().NotBeNull().And.Match<AppConfig>(x => x.Browser.SiteZoom.Count == 0);
    }

    [Fact]
    public void BadSavedZoomEntriesAreCleanedUp()
    {
        AppConfig c = Fresh(new BrowserConfig
        {
            SiteZoom = new Dictionary<string, double> { ["WWW.Cams.Example.Invalid."] = 1.25, [""] = 2, ["x/y"] = 2, ["one.example.invalid"] = 1.0, ["big.example.invalid"] = 50 },
        });
        c.Browser.SiteZoom.Should().BeEquivalentTo(new Dictionary<string, double> { ["cams.example.invalid"] = 1.25, ["big.example.invalid"] = BrowserRules.MaxZoom });
    }

    [Fact]
    public void ZoomedSitesAreRedactedFromLogs()
    {
        AppConfig c = BrowserRules.WithZoom(Fresh(), "https://brandname.example.invalid/x", 1.3);
        var redactor = new LogRedactor(Pseudonymizer.Ephemeral());
        redactor.Reset(ConfigRules.PseudonymizerFor(c), c.Urls());
        redactor.Redact("brandname zoom 130%").Should().NotContainEquivalentOf("brandname");
    }

    [Fact]
    public void TabsAreRedactedFromLogs()
    {
        AppConfig c = BrowserRules.WithTabs(Fresh(), ["https://brandname.example.invalid/live"], 0);
        c.Urls().Should().Contain("https://brandname.example.invalid/live");
    }

    // ---- Settings: defaults, every value, round trip ----

    [Fact]
    public void BrowserSettingsDefault()
    {
        BrowserConfig b = Fresh().Browser;
        (b.RestoreTabs, b.FocusNewTabs, b.RememberZoom).Should().Be((true, false, true));
        Fresh().OpenInBrowser.Should().Be(OpenInBrowserMode.KeepOnWall);
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(false, false, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, false)]
    public void EveryBrowserSettingRoundTrips(bool restore, bool focus, bool zoom)
    {
        AppConfig c = BrowserRules.WithZoom(
            BrowserRules.WithTabs(Fresh(new BrowserConfig { RestoreTabs = restore, FocusNewTabs = focus, RememberZoom = true }), [A, B], 1), A, 1.3);
        c = c with { Browser = c.Browser with { RememberZoom = zoom } };

        ConfigDocument.TryParse(ConfigDocument.Serialize(c), out AppConfig back, out _).Should().BeTrue();

        back.Browser.Tabs.Should().Equal(A, B);
        back.Browser.ActiveTab.Should().Be(1);
        (back.Browser.RestoreTabs, back.Browser.FocusNewTabs, back.Browser.RememberZoom).Should().Be((restore, focus, zoom));
        back.Browser.SiteZoom.Should().ContainKey("cams.example.invalid").WhoseValue.Should().Be(1.3);
    }

    [Theory]
    [InlineData(OpenInBrowserMode.KeepOnWall)]
    [InlineData(OpenInBrowserMode.MoveOffWall)]
    [InlineData(OpenInBrowserMode.Ask)]
    public void EveryOpenInBrowserModeRoundTrips(OpenInBrowserMode mode)
    {
        AppConfig c = ConfigRules.Normalize(new AppConfig { OpenInBrowser = mode });
        ConfigDocument.TryParse(ConfigDocument.Serialize(c), out AppConfig back, out _).Should().BeTrue();
        back.OpenInBrowser.Should().Be(mode);
    }

    [Fact]
    public void AnUnknownOpenInBrowserModeFallsBackToTheDefault()
    {
        ConfigDocument.TryParse("""{"openInBrowser":7}""", out AppConfig c, out _).Should().BeTrue();
        c.OpenInBrowser.Should().Be(OpenInBrowserMode.KeepOnWall);
    }
}
