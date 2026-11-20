using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StreamWall.App.Services;
using StreamWall.Core.Config;
using StreamWall.Core.Presentation;

namespace StreamWall.App.ViewModels;

public enum ControlPanelTab
{
    Walls,
    Browser,
    Streams,
    Settings,
}

/// <summary>The control panel: four tabs over one <see cref="AppHost"/>.</summary>
public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly AppHost _host;

    public MainWindowViewModel(AppHost host)
    {
        _host = host;
        Title = host.Demo ? "StreamWall (demo)" : host.Dev ? "StreamWall (dev)" : "StreamWall";
        Version = "v" + AppHost.Version + (host.Dev ? " dev" : "");
        host.UpdateChanged += s => Banner = UpdateBanner.From(s);
        host.OpenInBrowserRequested += OpenInBrowser;
        Walls = new WallsPageViewModel(host);
        Streams = new StreamsPageViewModel(host, (url, what) => SendToWallAsync(url, what));
        Settings = new SettingsPageViewModel(host);
        Browser = new BrowserViewModel(host, SendToWallAsync);
        host.ConfigChanged += Apply;
        if (host.IsLoaded)
            Apply(host.Config);
    }

    public string Title { get; }

    public string Version { get; }

    public WallsPageViewModel Walls { get; }

    public StreamsPageViewModel Streams { get; }

    public SettingsPageViewModel Settings { get; }

    public BrowserViewModel Browser { get; }

    /// <summary>Shows the Send to wall picker; set by the window (tests set a fake).</summary>
    public Func<string, Task<CellRef?>> PickCell { get; set; } = _ => Task.FromResult<CellRef?>(null);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWallsTab), nameof(IsBrowserTab), nameof(IsStreamsTab), nameof(IsSettingsTab))]
    private ControlPanelTab _selectedTab = ControlPanelTab.Walls;

    [ObservableProperty]
    private bool _isLoading = true;

    /// <summary>The update banner (found / downloading / ready / failed); null hides it.</summary>
    [ObservableProperty]
    private UpdateBanner? _banner;

    /// <summary>Restart into a downloaded update now (otherwise it installs on quit).</summary>
    [RelayCommand]
    private void InstallUpdate() => _host.Updates.Install();

    [ObservableProperty]
    private string _status = "Loading...";

    public bool IsWallsTab => SelectedTab == ControlPanelTab.Walls;

    public bool IsBrowserTab => SelectedTab == ControlPanelTab.Browser;

    public bool IsStreamsTab => SelectedTab == ControlPanelTab.Streams;

    public bool IsSettingsTab => SelectedTab == ControlPanelTab.Settings;

    [RelayCommand]
    private void ShowTab(ControlPanelTab tab) => SelectedTab = tab;

    /// <summary>The window should come to the front (Open in Browser from a wall).</summary>
    public event Action? ShowRequested;

    /// <summary>A wall's Open in Browser: a new tab in front, on the Browser tab, window to the front.</summary>
    private void OpenInBrowser(string url)
    {
        Browser.OpenTab(url, focus: true);
        SelectedTab = ControlPanelTab.Browser;
        ShowRequested?.Invoke();
    }

    /// <summary>Picker, then Send to wall. Returns where it went, or null if canceled.</summary>
    public async Task<CellRef?> SendToWallAsync(string url, string what, string? pageTitle = null)
    {
        CellRef? cell = await PickCell(what);
        if (cell is CellRef target)
            _host.SendToWall(url, target, pageTitle);
        return cell;
    }

    private void Apply(AppConfig config)
    {
        IsLoading = false;
        int streams = config.Streams.Count;
        int placed = config.Walls.Sum(w => w.Cells.Count(c => c is not null));
        Status = string.Create(CultureInfo.InvariantCulture,
            $"{config.Walls.Count} wall(s), {placed} cell(s) showing, {streams} stream(s) in the library.");
    }
}
