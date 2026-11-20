using StreamWall.Core.Config;
using StreamWall.Core.Presentation;
using StreamWall.Core.Updates;

namespace StreamWall.Core.Tests;

/// <summary>v0.2.1 settings: control panel on top, tab width; and the update status text.</summary>
public sealed class ControlPanelAndTabSettingsTests
{
    [Fact]
    public void OnTopDefaultsOffAndRoundTrips()
    {
        ConfigRules.Normalize(new AppConfig()).ControlPanelOnTop.Should().BeFalse();
        foreach (bool value in new[] { true, false })
        {
            AppConfig c = ConfigRules.Normalize(new AppConfig { ControlPanelOnTop = value });
            ConfigDocument.TryParse(ConfigDocument.Serialize(c), out AppConfig back, out _).Should().BeTrue();
            back.ControlPanelOnTop.Should().Be(value);
        }
    }

    [Theory]
    [InlineData(200, 200)]
    [InlineData(10, BrowserRules.MinTabWidth)]
    [InlineData(9999, BrowserRules.MaxTabWidth)]
    [InlineData(150, 150)]
    public void TabWidthIsClamped(int width, int expected) =>
        ConfigRules.Normalize(new AppConfig { Browser = new BrowserConfig { TabWidth = width } }).Browser.TabWidth.Should().Be(expected);

    [Fact]
    public void TabWidthDefaultsAndRoundTrips()
    {
        ConfigRules.Normalize(new AppConfig()).Browser.TabWidth.Should().Be(BrowserRules.DefaultTabWidth);
        AppConfig c = ConfigRules.Normalize(new AppConfig { Browser = new BrowserConfig { TabWidth = 240 } });
        ConfigDocument.TryParse(ConfigDocument.Serialize(c), out AppConfig back, out _).Should().BeTrue();
        back.Browser.TabWidth.Should().Be(240);
    }

    [Fact]
    public void AnOlderConfigGetsTheDefaultTabWidth()
    {
        ConfigDocument.TryParse("""{"browser":{"tabs":[]}}""", out AppConfig c, out _).Should().BeTrue();
        c.Browser.TabWidth.Should().Be(BrowserRules.DefaultTabWidth);
    }

    [Theory]
    [InlineData(UpdateStatus.Disabled, false, false)]
    [InlineData(UpdateStatus.Idle, true, false)]
    [InlineData(UpdateStatus.Checking, false, false)]
    [InlineData(UpdateStatus.NotAvailable, true, false)]
    [InlineData(UpdateStatus.Available, false, false)]
    [InlineData(UpdateStatus.Downloading, false, false)]
    [InlineData(UpdateStatus.Downloaded, false, true)]
    [InlineData(UpdateStatus.Error, true, false)]
    public void EveryUpdateStateHasTextAndTheRightButtons(UpdateStatus status, bool canCheck, bool canInstall)
    {
        UpdateStatusText text = UpdateStatusText.From(new UpdateState(status, "0.2.1", 40));
        text.Text.Should().NotBeNullOrWhiteSpace();
        (text.CanCheck, text.CanInstall).Should().Be((canCheck, canInstall));
    }

    [Fact]
    public void TheTextSaysWhatHappened()
    {
        UpdateStatusText.From(new UpdateState(UpdateStatus.NotAvailable)).Text.Should().Be("You're up to date.");
        UpdateStatusText.From(new UpdateState(UpdateStatus.Downloading, "0.2.1", 40)).Text.Should().Be("Downloading update v0.2.1... 40%");
        UpdateStatusText.From(new UpdateState(UpdateStatus.Downloaded, "0.2.1")).Text.Should().Contain("v0.2.1 is ready");
    }
}
