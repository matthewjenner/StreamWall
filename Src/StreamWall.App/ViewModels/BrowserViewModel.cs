using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StreamWall.App.Services;
using StreamWall.App.Web;
using StreamWall.Core.Config;
using StreamWall.Core.Diagnostics;
using StreamWall.Core.Presentation;

namespace StreamWall.App.ViewModels;

/// <summary>
/// The Browser tab: where the user signs in and finds streams. The view drives the web view and
/// reports the current page here; Send to wall and "This is the sign-in page" act on it.
/// </summary>
public sealed partial class BrowserViewModel : ObservableObject
{
    private readonly AppHost _host;
    private readonly Func<string, string, Task<CellRef?>> _send;

    public BrowserViewModel(AppHost host, Func<string, string, Task<CellRef?>> send)
    {
        _host = host;
        _send = send;
    }

    public WebEnvironment Web => _host.Web;

    public DiagLog Log => _host.Log;

    public string Home => _host.Home;

    /// <summary>What the address bar shows and the user edits.</summary>
    [ObservableProperty]
    private string _address = "";

    /// <summary>The page the web view is on (from the view).</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendToWallCommand), nameof(MarkSignInPageCommand))]
    private string? _currentUrl;

    [ObservableProperty]
    private string? _message;

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

    [RelayCommand(CanExecute = nameof(CanUseCurrentPage))]
    private async Task SendToWall()
    {
        if (CurrentUrl is not { } url)
            return;
        CellRef? cell = await _send(url, "this page");
        if (cell is CellRef target)
            Message = "Sent to " + WallMap.From(_host.Config).Describe(target) + ".";
    }

    [RelayCommand(CanExecute = nameof(CanUseCurrentPage))]
    private void MarkSignInPage()
    {
        if (CurrentUrl is not { } url || !Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
            return;
        string? pattern = uri.AbsolutePath.Length > 1 ? uri.AbsolutePath : null;
        _host.Update(c => c with { Site = c.Site with { SignInUrl = url, SignedOutUrlPattern = pattern ?? c.Site.SignedOutUrlPattern } });
        _host.Log.SignInPageSet(pattern is not null);
        Message = pattern is null
            ? "Saved as the sign-in page."
            : "Saved as the sign-in page. Cells that land on " + pattern + " will show Signed out.";
    }
}
