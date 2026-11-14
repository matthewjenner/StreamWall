using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StreamWall.App.Services;
using StreamWall.Core.Config;

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
        if (host.IsLoaded)
            Apply(host.Config);
    }

    [ObservableProperty]
    private decimal _stallTimeoutSeconds = ConfigRules.DefaultStallTimeoutSeconds;

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
        }
        finally
        {
            _applying = false;
        }
    }
}
