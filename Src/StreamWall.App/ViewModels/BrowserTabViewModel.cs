using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace StreamWall.App.ViewModels;

/// <summary>
/// One Browser tab. The view owns its web view and keeps these in step with it (page, title,
/// audio, history, zoom); the toolbar and tab strip act through the requests below.
/// </summary>
public sealed partial class BrowserTabViewModel(string url) : ObservableObject
{
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>The tab's page (where it opens; then wherever the user navigates).</summary>
    [ObservableProperty]
    private string _url = url;

    /// <summary>The page's own title (null until it has one): what Send to wall uses as a label.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title))]
    private string? _pageTitle;

    /// <summary>What the tab strip shows.</summary>
    public string Title => string.IsNullOrWhiteSpace(PageTitle) ? "New tab" : PageTitle;

    [ObservableProperty]
    private bool _isPlayingAudio;

    [ObservableProperty]
    private bool _isMuted;

    [ObservableProperty]
    private bool _canGoBack;

    [ObservableProperty]
    private bool _canGoForward;

    /// <summary>The page's zoom, in percent.</summary>
    [ObservableProperty]
    private int _zoomPercent = 100;

    [ObservableProperty]
    private bool _isSelected;

    /// <summary>Load in the background now (a tab opened from a link), rather than when first shown.</summary>
    public bool LoadNow { get; init; }

    /// <summary>The toolbar asked for a zoom factor.</summary>
    public event Action<double>? ZoomRequested;

    public void RequestZoom(double factor) => ZoomRequested?.Invoke(factor);

    [RelayCommand]
    private void ToggleMute() => IsMuted = !IsMuted;
}
