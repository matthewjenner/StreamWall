using StreamWall.App.Services;
using StreamWall.Core.Config;

namespace StreamWall.App.Tests;

public sealed class MainWindowUiTests
{
    [Fact]
    public Task TheDemoConfigLoadsIntoTheControlPanel() => TestApp.Run(() =>
    {
        using UiHarness ui = UiHarness.Start();
        ui.TextNamed("StatusText").Text.Should().Be("3 streams configured.");
        ui.Window.Title.Should().Be("StreamWall (demo)");
    });

    [Fact]
    public Task AnEmptyConfigSaysSo() => TestApp.Run(() =>
    {
        using UiHarness ui = UiHarness.Start(new AppConfig());
        ui.TextNamed("StatusText").Text.Should().Be("No streams yet.");
    });

    [Fact]
    public Task StartingUpLogsNoUrlsOrLabels() => TestApp.Run(() =>
    {
        using UiHarness ui = UiHarness.Start();
        var sink = (MemoryDiagSink)ui.Host.Sink;
        string log = string.Join("\n", sink.Lines);
        log.Should().Contain("config loaded; 3 stream(s)");
        log.Should().NotContain("example.invalid").And.NotContain("Demo A").And.NotContain("http");
    });
}
