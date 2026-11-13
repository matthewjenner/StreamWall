using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StreamWall.App.Services;
using StreamWall.Core.Config;
using StreamWall.Core.Presentation;

namespace StreamWall.App.ViewModels;

/// <summary>The Settings tab: the sign-in page (masked), signed-out indicators, stall timeout, pseudonyms.</summary>
public sealed partial class SettingsPageViewModel : ObservableObject
{
    private readonly AppHost _host;
    private bool _applying;
    private string? _signInUrl;

    public SettingsPageViewModel(AppHost host)
    {
        _host = host;
        host.ConfigChanged += Apply;
        if (host.IsLoaded)
            Apply(host.Config);
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SignInDisplay), nameof(RevealText))]
    private bool _revealed;

    [ObservableProperty]
    private string? _signedOutPattern;

    [ObservableProperty]
    private string? _signedOutSelector;

    [ObservableProperty]
    private decimal _stallTimeoutSeconds = ConfigRules.DefaultStallTimeoutSeconds;

    [ObservableProperty]
    private string? _message;

    public string SignInDisplay => _signInUrl is null
        ? "Not set - open it in the Browser tab and click \"This is the sign-in page\"."
        : Revealed ? _signInUrl : UrlMask.Mask(_signInUrl);

    public string RevealText => Revealed ? "Hide" : "Reveal";

    public static int MinStall => ConfigRules.MinStallTimeoutSeconds;

    public static int MaxStall => ConfigRules.MaxStallTimeoutSeconds;

    [RelayCommand]
    private void ToggleReveal() => Revealed = !Revealed;

    [RelayCommand]
    private void ClearSignIn() => _host.Update(c => c with { Site = c.Site with { SignInUrl = null, SignedOutUrlPattern = null } });

    [RelayCommand]
    private async Task ResetPseudonyms()
    {
        await _host.ResetPseudonymsAsync();
        Message = "Every site and stream has a new name in the logs from now on.";
    }

    partial void OnSignedOutPatternChanged(string? value) => Change(s => s with { SignedOutUrlPattern = value });

    partial void OnSignedOutSelectorChanged(string? value) => Change(s => s with { SignedOutSelector = value });

    partial void OnStallTimeoutSecondsChanged(decimal value) => Change(s => s with { StallTimeoutSeconds = (int)value });

    private void Change(Func<SiteConfig, SiteConfig> change)
    {
        if (!_applying)
            _host.Update(c => c with { Site = change(c.Site) });
    }

    private void Apply(AppConfig config)
    {
        _applying = true;
        try
        {
            _signInUrl = config.Site.SignInUrl;
            SignedOutPattern = config.Site.SignedOutUrlPattern;
            SignedOutSelector = config.Site.SignedOutSelector;
            StallTimeoutSeconds = config.Site.StallTimeoutSeconds;
            OnPropertyChanged(nameof(SignInDisplay));
        }
        finally
        {
            _applying = false;
        }
    }
}
