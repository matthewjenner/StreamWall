using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StreamWall.Core.Config;

namespace StreamWall.App.ViewModels;

/// <summary>
/// One cell on a wall: which stream it shows and that stream's audio settings. The view owns the
/// web view; this holds what it should show. Changes the user makes go back through callbacks to
/// the host (and so to the saved config); <see cref="Apply"/> brings the config's state in without
/// echoing it back.
/// </summary>
public sealed partial class CellViewModel : ObservableObject
{
    private readonly Action<CellViewModel> _clear;
    private readonly Action<Guid, int> _setVolume;
    private readonly Action<Guid, bool> _setMuted;
    private bool _applying;

    public CellViewModel(CellRef cell, int wallNumber, Action<CellViewModel> clear, Action<Guid, int> setVolume, Action<Guid, bool> setMuted)
    {
        Ref = cell;
        WallNumber = wallNumber;
        _clear = clear;
        _setVolume = setVolume;
        _setMuted = setMuted;
    }

    public CellRef Ref { get; }

    public int Number => Ref.Index + 1;

    public int WallNumber { get; set; }

    /// <summary>How long to wait before first loading, so a wall's cells do not all hit the site at once.</summary>
    public TimeSpan StartDelay { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private Guid? _streamId;

    [ObservableProperty]
    private string _label = "Empty";

    /// <summary>The page to load. Never logged and never shown on the wall.</summary>
    [ObservableProperty]
    private string? _url;

    [ObservableProperty]
    private string? _videoSelector;

    [ObservableProperty]
    private string? _signedOutSelector;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EffectiveVolume))]
    private int _volume = ConfigRules.DefaultVolume;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EffectiveMuted))]
    private bool _muted;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EffectiveVolume))]
    private int _masterVolume = 100;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EffectiveMuted))]
    private bool _masterMuted;

    /// <summary>"loading", "playing", "waiting", "no video", "error" - drives the strip's dot.</summary>
    [ObservableProperty]
    private string _status = "";

    public bool IsEmpty => StreamId is null;

    /// <summary>0-1: the stream's volume scaled by the master volume.</summary>
    public double EffectiveVolume => Volume / 100.0 * (MasterVolume / 100.0);

    public bool EffectiveMuted => Muted || MasterMuted;

    public void Apply(AppConfig config, Guid? streamId)
    {
        _applying = true;
        try
        {
            StreamConfig? stream = streamId is Guid id ? config.Streams.FirstOrDefault(s => s.Id == id) : null;
            StreamId = stream?.Id;
            Label = stream?.Label ?? "Empty";
            Url = stream?.Url;
            VideoSelector = stream?.VideoSelector;
            SignedOutSelector = config.Site.SignedOutSelector;
            Volume = stream?.Volume ?? ConfigRules.DefaultVolume;
            Muted = stream?.Muted ?? false;
            MasterVolume = config.MasterVolume;
            MasterMuted = config.MasterMuted;
            if (stream is null)
                Status = "";
        }
        finally
        {
            _applying = false;
        }
    }

    [RelayCommand]
    private void Clear() => _clear(this);

    [RelayCommand]
    private void ToggleMute() => Muted = !Muted;

    partial void OnVolumeChanged(int value)
    {
        if (!_applying && StreamId is Guid id)
            _setVolume(id, value);
    }

    partial void OnMutedChanged(bool value)
    {
        if (!_applying && StreamId is Guid id)
            _setMuted(id, value);
    }
}
