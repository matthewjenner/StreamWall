using Avalonia.Controls;
using Avalonia.VisualTree;
using StreamWall.Core.Presentation;
using StreamWall.Core.Updates;

namespace StreamWall.App.Tests;

public sealed class UpdateBannerUiTests
{
    [Fact]
    public Task ADemoOrDevCopyShowsNoUpdateBanner() => TestApp.Run(() =>
    {
        using UiHarness ui = UiHarness.Start();
        ui.WaitUntil(() => ui.Host.Updates.State.Status == UpdateStatus.Disabled, "updates to report disabled");
        ui.ViewModel.Banner.Should().BeNull();
        ui.Window.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "UpdateBanner").IsVisible.Should().BeFalse();
        ui.LogText.Should().Contain("auto-update disabled");
    });

    [Fact]
    public Task AReadyUpdateShowsRestartAndInstall() => TestApp.Run(() =>
    {
        using UiHarness ui = UiHarness.Start();
        ui.ViewModel.Banner = UpdateBanner.From(new UpdateState(UpdateStatus.Downloaded, "0.1.1"));
        ui.Settle();
        ui.Find<TextBlock>(t => t.Text == "Update v0.1.1 ready to install.", "the banner text").Should().NotBeNull();
        ui.Named<Button>("InstallUpdateButton").IsEffectivelyVisible.Should().BeTrue();
    });
}
