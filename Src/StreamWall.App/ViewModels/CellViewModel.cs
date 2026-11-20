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
    private readonly ICellActions _actions;
    private bool _applying;

    public CellViewModel(CellRef cell, int wallNumber, ICellActions actions)
    {
        Ref = cell;
        WallNumber = wallNumber;
        _actions = actions;
    }

    /// <summary>Raised to reload the web view now (or after the given delay, for staggered reloads).</summary>
    public event Action<TimeSpan>? ReloadRequested;

    /// <summary>Reloads this cell's page (the strip's Reload; Reload all staggers with a delay).</summary>
    public void RequestReload(TimeSpan delay) => ReloadRequested?.Invoke(delay);

    /// <summary>The view reports the page's title; the label may follow it (Settings, <see cref="StreamLabels"/>).</summary>
    public void ReportPageTitle(string? title)
    {
        if (StreamId is Guid id)
            _actions.PageTitle(id, title);
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
    private void Clear() => _actions.Clear(this);

    [RelayCommand]
    private void Reload()
    {
        if (IsEmpty)
            return;
        _actions.Reloaded(this);
        RequestReload(TimeSpan.Zero);
    }

    /// <summary>Picks another cell (any wall) and trades places with it.</summary>
    [RelayCommand]
    private Task Swap() => IsEmpty ? Task.CompletedTask : _actions.SwapAsync(this);

    /// <summary>Opens the page in a new Browser tab (Settings: keep on wall, move off, or ask).</summary>
    [RelayCommand]
    private Task OpenInBrowser() => IsEmpty ? Task.CompletedTask : _actions.OpenInBrowserAsync(this);

    [RelayCommand]
    private void ToggleMute() => Muted = !Muted;

    partial void OnVolumeChanged(int value)
    {
        if (!_applying && StreamId is Guid id)
            _actions.SetVolume(id, value);
    }

    partial void OnMutedChanged(bool value)
    {
        if (!_applying && StreamId is Guid id)
            _actions.SetMuted(id, value);
    }
}

/// <summary>What a cell asks of its wall (implemented by <see cref="WallViewModel"/>).</summary>
public interface ICellActions
{
    void Clear(CellViewModel cell);

    void SetVolume(Guid stream, int volume);

    void SetMuted(Guid stream, bool muted);

    /// <summary>The user asked to reload this cell (logged).</summary>
    void Reloaded(CellViewModel cell);

    /// <summary>Picks a cell to swap with, then swaps.</summary>
    Task SwapAsync(CellViewModel cell);

    /// <summary>Opens the cell's page in a Browser tab, per the Open in Browser setting.</summary>
    Task OpenInBrowserAsync(CellViewModel cell);

    /// <summary>The cell's page reported its title.</summary>
    void PageTitle(Guid stream, string? title);
}
