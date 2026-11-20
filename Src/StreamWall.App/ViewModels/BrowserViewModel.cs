using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StreamWall.App.Services;
using StreamWall.App.Web;
using StreamWall.Core.Config;
using StreamWall.Core.Diagnostics;
using StreamWall.Core.Presentation;

namespace StreamWall.App.ViewModels;

/// <summary>
/// The Browser tab: where the user signs in (like any browser; the session is shared with every
/// cell) and finds streams. The view drives the web view and reports the current page here; Send to
/// wall acts on it. The tab reopens at its last page.
/// </summary>
public sealed partial class BrowserViewModel : ObservableObject
{
    private readonly AppHost _host;
    private readonly Func<string, string, string?, Task<CellRef?>> _send;

    public BrowserViewModel(AppHost host, Func<string, string, string?, Task<CellRef?>> send)
    {
        _host = host;
        _send = send;
    }

    public WebEnvironment Web => _host.Web;

    public DiagLog Log => _host.Log;

    /// <summary>Where the tab opens: its last page (or the demo index, or blank).</summary>
    public string StartPage => _host.BrowserStart;

    /// <summary>What the address bar shows and the user edits.</summary>
    [ObservableProperty]
    private string _address = "";

    /// <summary>The page the web view is on (from the view).</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendToWallCommand))]
    private string? _currentUrl;

    [ObservableProperty]
    private string? _message;

    /// <summary>The current page's title (from the view): the default label when it is sent to a wall.</summary>
    [ObservableProperty]
    private string? _currentTitle;

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

    partial void OnCurrentUrlChanged(string? value)
    {
        if (value is not null)
            _host.RememberBrowserPage(value);
    }

    [RelayCommand(CanExecute = nameof(CanUseCurrentPage))]
    private async Task SendToWall()
    {
        if (CurrentUrl is not { } url)
            return;
        CellRef? cell = await _send(url, "this page", CurrentTitle);
        if (cell is CellRef target)
            Message = "Sent to " + WallMap.From(_host.Config).Describe(target) + ".";
    }
}
