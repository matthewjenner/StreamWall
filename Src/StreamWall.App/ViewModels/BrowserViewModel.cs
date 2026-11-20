using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StreamWall.App.Services;
using StreamWall.App.Web;
using StreamWall.Core.Config;
using StreamWall.Core.Diagnostics;
using StreamWall.Core.Presentation;

namespace StreamWall.App.ViewModels;

/// <summary>
/// The Browser tab: tabs on the shared profile, where the user signs in (like any browser; every
/// cell shares the session) and finds streams. Tabs are saved as they change and restored on
/// launch (Settings). Send to wall and zoom act on the selected tab.
/// </summary>
public sealed partial class BrowserViewModel : ObservableObject
{
    /// <summary>Browser-like zoom steps for the toolbar's - and + (Ctrl+wheel inside the page is finer).</summary>
    public static IReadOnlyList<double> ZoomSteps { get; } = [0.25, 0.33, 0.5, 0.67, 0.75, 0.8, 0.9, 1.0, 1.1, 1.25, 1.5, 1.75, 2.0, 2.5, 3.0, 4.0, 5.0];

    private readonly AppHost _host;
    private readonly Func<string, string, string?, Task<CellRef?>> _send;
    private BrowserTabViewModel? _watched;
    private bool _restored;

    public BrowserViewModel(AppHost host, Func<string, string, string?, Task<CellRef?>> send)
    {
        _host = host;
        _send = send;
        host.ConfigChanged += _ =>
        {
            RestoreOnce();
            OnPropertyChanged(nameof(TabWidth));
        };
        if (host.IsLoaded)
            RestoreOnce();
    }

    public WebEnvironment Web => _host.Web;

    public DiagLog Log => _host.Log;

    /// <summary>A new tab's page.</summary>
    public string StartPage => _host.NewTabPage;

    /// <summary>Tabs opened from links come to the front (Settings); otherwise they open in the background.</summary>
    public bool FocusNewTabs => _host.Config.Browser.FocusNewTabs;

    /// <summary>Every tab's width in the strip (Settings): fixed, so tabs never move as titles change.</summary>
    public int TabWidth => _host.Config.Browser.TabWidth;

    public ObservableCollection<BrowserTabViewModel> Tabs { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentUrl), nameof(CurrentTitle), nameof(CanUseCurrentPage))]
    [NotifyCanExecuteChangedFor(nameof(SendToWallCommand))]
    private BrowserTabViewModel? _selectedTab;

    [ObservableProperty]
    private string? _message;

    /// <summary>The selected tab's page (setting it navigates nowhere; the view reports pages here).</summary>
    public string? CurrentUrl
    {
        get => SelectedTab?.Url;
        set => (SelectedTab ?? OpenTab(StartPage, focus: true)).Url = value ?? "";
    }

    /// <summary>The selected tab's page title: the default label when it is sent to a wall.</summary>
    public string? CurrentTitle
    {
        get => SelectedTab?.PageTitle;
        set => (SelectedTab ?? OpenTab(StartPage, focus: true)).PageTitle = value;
    }

    public bool CanUseCurrentPage => ConfigRules.IsValidUrl(CurrentUrl);

    /// <summary>Turns what was typed into an address: as typed if it has a scheme, else https://.</summary>
    public static string? ToAddress(string typed)
    {
        string t = typed.Trim();
        if (t.Length == 0 || t.Contains(' ', StringComparison.Ordinal))
            return null;
        if (Uri.TryCreate(t, UriKind.Absolute, out Uri? uri) && uri.Scheme is "http" or "https" or "about")
            return t;
        string https = "https://" + t;
        return t.Contains('.', StringComparison.Ordinal) && ConfigRules.IsValidUrl(https) ? https : null;
    }

    /// <summary>
    /// Opens a tab after the selected one. <paramref name="focus"/> selects it; otherwise it loads in
    /// the background (a link opened in a new tab).
    /// </summary>
    public BrowserTabViewModel OpenTab(string? url, bool focus)
    {
        var tab = new BrowserTabViewModel(string.IsNullOrWhiteSpace(url) ? StartPage : url) { LoadNow = !focus };
        int at = SelectedTab is null ? Tabs.Count : Tabs.IndexOf(SelectedTab) + 1;
        Tabs.Insert(at, tab);
        if (focus || SelectedTab is null)
            SelectedTab = tab;
        SaveTabs();
        return tab;
    }

    [RelayCommand]
    private void NewTab() => OpenTab(StartPage, focus: true);

    /// <summary>Closes a tab (its x or a middle-click); the last tab closing leaves a fresh one, like a browser.</summary>
    [RelayCommand]
    private void CloseTab(BrowserTabViewModel tab)
    {
        int index = Tabs.IndexOf(tab);
        if (index < 0)
            return;
        bool wasSelected = tab == SelectedTab;
        Tabs.RemoveAt(index);
        if (Tabs.Count == 0)
        {
            SelectedTab = null;
            OpenTab(StartPage, focus: true);
            return;
        }
        if (wasSelected)
            SelectedTab = Tabs[Math.Min(index, Tabs.Count - 1)];
        SaveTabs();
    }

    [RelayCommand]
    private void SelectTab(BrowserTabViewModel tab) => SelectedTab = tab;

    [RelayCommand]
    private void ZoomIn() => StepZoom(+1);

    [RelayCommand]
    private void ZoomOut() => StepZoom(-1);

    [RelayCommand]
    private void ZoomReset() => SelectedTab?.RequestZoom(1.0);

    private void StepZoom(int direction)
    {
        if (SelectedTab is { } tab)
            tab.RequestZoom(NextZoom(tab.ZoomPercent, direction));
    }

    /// <summary>The next browser zoom step up or down from the selected tab's zoom.</summary>
    public static double NextZoom(int percent, int direction)
    {
        double current = percent / 100.0;
        return direction > 0
            ? ZoomSteps.FirstOrDefault(s => s > current + 0.001, ZoomSteps[^1])
            : ZoomSteps.LastOrDefault(s => s < current - 0.001, ZoomSteps[0]);
    }

    /// <summary>The zoom to apply to a page (its site's), or null when zoom is not remembered.</summary>
    public double? ZoomFor(string? url) => _host.ZoomFor(url);

    /// <summary>A tab's zoom changed (Ctrl+wheel, the toolbar): remember it for the site.</summary>
    public void SaveZoom(string? url, double zoom) => _host.SaveZoom(url, zoom);

    [RelayCommand(CanExecute = nameof(CanUseCurrentPage))]
    private async Task SendToWall()
    {
        if (CurrentUrl is not { } url)
            return;
        CellRef? cell = await _send(url, "this page", CurrentTitle);
        if (cell is CellRef target)
            Message = "Sent to " + WallMap.From(_host.Config).Describe(target) + ".";
    }

    partial void OnSelectedTabChanged(BrowserTabViewModel? oldValue, BrowserTabViewModel? newValue)
    {
        if (oldValue is not null)
            oldValue.IsSelected = false;
        if (newValue is not null)
            newValue.IsSelected = true;
        if (_watched is not null)
            _watched.PropertyChanged -= OnSelectedTabPropertyChanged;
        _watched = newValue;
        if (_watched is not null)
            _watched.PropertyChanged += OnSelectedTabPropertyChanged;
        Message = null;
        SaveTabs();
    }

    private void OnSelectedTabPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(BrowserTabViewModel.Url))
        {
            OnPropertyChanged(nameof(CurrentUrl));
            OnPropertyChanged(nameof(CanUseCurrentPage));
            SendToWallCommand.NotifyCanExecuteChanged();
        }
        else if (e.PropertyName == nameof(BrowserTabViewModel.PageTitle))
        {
            OnPropertyChanged(nameof(CurrentTitle));
        }
    }

    /// <summary>Saves the tab list (restored on launch per Settings); background tabs report pages too.</summary>
    public void SaveTabs()
    {
        if (_restored)
            _host.SaveTabs([.. Tabs.Select(t => t.Url)], SelectedTab is null ? 0 : Tabs.IndexOf(SelectedTab));
    }

    private void RestoreOnce()
    {
        if (_restored || !_host.IsLoaded)
            return;
        (IReadOnlyList<string> urls, int active) = BrowserRules.StartTabs(_host.Config);
        foreach (string url in urls)
            Tabs.Add(new BrowserTabViewModel(url));
        if (Tabs.Count == 0)
            Tabs.Add(new BrowserTabViewModel(StartPage));
        _restored = true;
        SelectedTab = Tabs[Math.Clamp(active, 0, Tabs.Count - 1)];
    }
}
