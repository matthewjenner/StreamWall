using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StreamWall.App.Services;
using StreamWall.Core.Config;
using StreamWall.Core.Presentation;
using StreamWall.Core.Updates;

namespace StreamWall.App.ViewModels;

/// <summary>
/// The Settings tab: the stall timeout and pseudonyms. There is no sign-in setting: signing in is
/// done in the Browser tab, and cells that lose their video retry on their own.
/// </summary>
public sealed partial class SettingsPageViewModel : ObservableObject
{
    private readonly AppHost _host;
    private bool _applying;

    public SettingsPageViewModel(AppHost host)
    {
        _host = host;
        host.ConfigChanged += Apply;
        host.UpdateChanged += ShowUpdate;
        ShowUpdate(host.Updates.State);
        if (host.IsLoaded)
            Apply(host.Config);
    }

    // ---- Updates ----

    public string VersionText => "StreamWall v" + AppHost.Version + (_host.Dev ? " (dev copy)" : _host.Demo ? " (demo)" : "");

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CheckForUpdatesCommand), nameof(InstallUpdateCommand))]
    private UpdateStatusText _update = UpdateStatusText.From(new UpdateState(UpdateStatus.Idle));

    private bool CanCheck => Update.CanCheck;

    private bool CanInstall => Update.CanInstall;

    /// <summary>Checks now (and downloads what it finds) instead of waiting for the hourly check.</summary>
    [RelayCommand(CanExecute = nameof(CanCheck))]
    private Task CheckForUpdates() => _host.Updates.CheckAsync();

    [RelayCommand(CanExecute = nameof(CanInstall))]
    private void InstallUpdate() => _host.Updates.Install();

    private void ShowUpdate(UpdateState state) => Update = UpdateStatusText.From(state);

    // ---- Control panel and Browser tab layout ----

    [ObservableProperty]
    private bool _controlPanelOnTop;

    partial void OnControlPanelOnTopChanged(bool value)
    {
        if (!_applying)
            _host.Update(c => c with { ControlPanelOnTop = value });
    }

    [ObservableProperty]
    private decimal _tabWidth = BrowserRules.DefaultTabWidth;

    public static int MinTabWidth => BrowserRules.MinTabWidth;

    public static int MaxTabWidth => BrowserRules.MaxTabWidth;

    partial void OnTabWidthChanged(decimal value) => ChangeBrowser(b => b with { TabWidth = (int)value });

    [ObservableProperty]
    private decimal _stallTimeoutSeconds = ConfigRules.DefaultStallTimeoutSeconds;

    /// <summary>The Stream labels choices, in display order.</summary>
    public static IReadOnlyList<LabelModeChoice> LabelModes { get; } =
    [
        new(StreamLabelMode.PageTitle, "Page title (also replaces \"Stream N\" labels)"),
        new(StreamLabelMode.PageTitleNewOnly, "Page title, new streams only"),
        new(StreamLabelMode.FollowPageTitle, "Keep following the page title"),
        new(StreamLabelMode.Numbered, "Numbered (Stream 1, Stream 2, ...)"),
    ];

    [ObservableProperty]
    private LabelModeChoice _labelMode = LabelModes[0];

    partial void OnLabelModeChanged(LabelModeChoice value)
    {
        if (!_applying)
            _host.Update(c => c with { LabelMode = value.Mode });
    }

    /// <summary>The Open in Browser choices, in display order.</summary>
    public static IReadOnlyList<OpenInBrowserChoiceItem> OpenInBrowserModes { get; } =
    [
        new(OpenInBrowserMode.KeepOnWall, "Open a tab, keep it on the wall"),
        new(OpenInBrowserMode.MoveOffWall, "Open a tab, move it off the wall"),
        new(OpenInBrowserMode.Ask, "Ask each time"),
    ];

    [ObservableProperty]
    private OpenInBrowserChoiceItem _openInBrowser = OpenInBrowserModes[0];

    [ObservableProperty]
    private bool _restoreTabs = true;

    [ObservableProperty]
    private bool _rememberControlPanel = true;

    partial void OnRememberControlPanelChanged(bool value)
    {
        if (!_applying)
            _host.Update(c => c with { RememberControlPanel = value });
    }

    [ObservableProperty]
    private bool _focusNewTabs;

    [ObservableProperty]
    private bool _rememberZoom = true;

    [ObservableProperty]
    private int _savedZoomSites;

    partial void OnOpenInBrowserChanged(OpenInBrowserChoiceItem value)
    {
        if (!_applying)
            _host.Update(c => c with { OpenInBrowser = value.Mode });
    }

    partial void OnRestoreTabsChanged(bool value) => ChangeBrowser(b => b with { RestoreTabs = value });

    partial void OnFocusNewTabsChanged(bool value) => ChangeBrowser(b => b with { FocusNewTabs = value });

    partial void OnRememberZoomChanged(bool value) => ChangeBrowser(b => b with { RememberZoom = value });

    /// <summary>Forgets every site's saved zoom (pages open at 100% again).</summary>
    [RelayCommand]
    private void ForgetZoom() => ChangeBrowser(b => b with { SiteZoom = new Dictionary<string, double>() });

    private void ChangeBrowser(Func<BrowserConfig, BrowserConfig> change)
    {
        if (!_applying)
            _host.Update(c => c with { Browser = change(c.Browser) });
    }

    /// <summary>0-100: what a newly sent or pasted stream starts at.</summary>
    [ObservableProperty]
    private decimal _newStreamVolume = ConfigRules.DefaultVolume;

    partial void OnNewStreamVolumeChanged(decimal value)
    {
        if (!_applying)
            _host.Update(c => c with { NewStreamVolume = (int)value });
    }

    [ObservableProperty]
    private string? _message;

    [RelayCommand]
    private async Task ResetPseudonyms()
    {
        await _host.ResetPseudonymsAsync();
        Message = "Every site and stream has a new name in the logs from now on.";
    }

    partial void OnStallTimeoutSecondsChanged(decimal value)
    {
        if (!_applying)
            _host.Update(c => c with { Site = c.Site with { StallTimeoutSeconds = (int)value } });
    }

    private void Apply(AppConfig config)
    {
        _applying = true;
        try
        {
            StallTimeoutSeconds = config.Site.StallTimeoutSeconds;
            NewStreamVolume = config.NewStreamVolume;
            LabelMode = LabelModes.FirstOrDefault(m => m.Mode == config.LabelMode) ?? LabelModes[0];
            OpenInBrowser = OpenInBrowserModes.FirstOrDefault(m => m.Mode == config.OpenInBrowser) ?? OpenInBrowserModes[0];
            RestoreTabs = config.Browser.RestoreTabs;
            RememberControlPanel = config.RememberControlPanel;
            ControlPanelOnTop = config.ControlPanelOnTop;
            TabWidth = config.Browser.TabWidth;
            FocusNewTabs = config.Browser.FocusNewTabs;
            RememberZoom = config.Browser.RememberZoom;
            SavedZoomSites = config.Browser.SiteZoom.Count;
        }
        finally
        {
            _applying = false;
        }
    }
}

/// <summary>One Stream labels option for the Settings ComboBox.</summary>
public sealed record LabelModeChoice(StreamLabelMode Mode, string Text)
{
    public override string ToString() => Text;
}

/// <summary>One Open in Browser option for the Settings ComboBox.</summary>
public sealed record OpenInBrowserChoiceItem(OpenInBrowserMode Mode, string Text)
{
    public override string ToString() => Text;
}
