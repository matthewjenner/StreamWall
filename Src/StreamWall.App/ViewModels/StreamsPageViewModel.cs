using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StreamWall.App.Services;
using StreamWall.Core.Config;
using StreamWall.Core.Presentation;

namespace StreamWall.App.ViewModels;

/// <summary>
/// The Streams tab: the library. URLs are masked until revealed per row. Streams normally arrive
/// by Send to wall from the Browser tab; pasting a URL is the fallback.
/// </summary>
public sealed partial class StreamsPageViewModel : ObservableObject
{
    private readonly AppHost _host;
    private readonly Func<string, string, Task<CellRef?>> _send;

    public StreamsPageViewModel(AppHost host, Func<string, string, Task<CellRef?>> send)
    {
        _host = host;
        _send = send;
        host.ConfigChanged += Apply;
        if (host.IsLoaded)
            Apply(host.Config);
    }

    public ObservableCollection<StreamRowViewModel> Streams { get; } = [];

    [ObservableProperty]
    private bool _isEmpty = true;

    [ObservableProperty]
    private string _pasteUrl = "";

    [ObservableProperty]
    private string? _pasteError;

    [RelayCommand]
    private void Add()
    {
        string url = PasteUrl.Trim();
        if (!ConfigRules.IsValidUrl(url))
        {
            PasteError = "Enter a full address starting with https://";
            return;
        }
        PasteError = null;
        _host.Update(c => WallLayout.AddOrReuseStream(c, url).Config);
        PasteUrl = "";
    }

    private void Apply(AppConfig config)
    {
        IsEmpty = config.Streams.Count == 0;
        WallMap map = WallMap.From(config);
        for (int i = Streams.Count - 1; i >= 0; i--)
        {
            if (config.Streams.All(s => s.Id != Streams[i].Id))
                Streams.RemoveAt(i);
        }
        for (int i = 0; i < config.Streams.Count; i++)
        {
            StreamConfig stream = config.Streams[i];
            StreamRowViewModel? row = Streams.FirstOrDefault(r => r.Id == stream.Id);
            if (row is null)
            {
                row = new StreamRowViewModel(_host, stream.Id, _send);
                Streams.Insert(Math.Min(i, Streams.Count), row);
            }
            var placed = map.Walls.SelectMany(w => w.Cells).Where(c => c.StreamId == stream.Id).Select(c => map.Describe(c.Ref)).ToList();
            row.Apply(stream, placed.Count == 0 ? "Not on a wall" : "On " + string.Join("; ", placed));
        }
    }
}

public sealed partial class StreamRowViewModel : ObservableObject
{
    private readonly AppHost _host;
    private readonly Func<string, string, Task<CellRef?>> _send;
    private bool _applying;
    private string _url = "";

    public StreamRowViewModel(AppHost host, Guid id, Func<string, string, Task<CellRef?>> send)
    {
        _host = host;
        _send = send;
        Id = id;
    }

    public Guid Id { get; }

    [ObservableProperty]
    private string _label = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayUrl), nameof(RevealText))]
    private bool _revealed;

    [ObservableProperty]
    private int _volume;

    [ObservableProperty]
    private bool _muted;

    [ObservableProperty]
    private string _placement = "";

    [ObservableProperty]
    private string? _videoSelector;

    public string DisplayUrl => Revealed ? _url : UrlMask.Mask(_url);

    public string RevealText => Revealed ? "Hide" : "Reveal";

    public void Apply(StreamConfig stream, string placement)
    {
        _applying = true;
        try
        {
            _url = stream.Url;
            Label = stream.Label;
            Volume = stream.Volume;
            Muted = stream.Muted;
            VideoSelector = stream.VideoSelector;
            Placement = placement;
            OnPropertyChanged(nameof(DisplayUrl));
        }
        finally
        {
            _applying = false;
        }
    }

    [RelayCommand]
    private void ToggleReveal() => Revealed = !Revealed;

    [RelayCommand]
    private void Remove() => _host.Update(c => WallLayout.RemoveStream(c, Id));

    [RelayCommand]
    private Task Send() => _send(_url, "\"" + Label + "\"");

    // Typed by the user: page titles never replace it from now on.
    partial void OnLabelChanged(string value) =>
        Change(s => string.IsNullOrWhiteSpace(value) ? s : s with { Label = value.Trim(), LabelEdited = true });

    partial void OnVolumeChanged(int value) => Change(s => s with { Volume = value });

    partial void OnMutedChanged(bool value) => Change(s => s with { Muted = value });

    partial void OnVideoSelectorChanged(string? value) => Change(s => s with { VideoSelector = value });

    private void Change(Func<StreamConfig, StreamConfig> change)
    {
        if (!_applying)
            _host.Update(c => WallLayout.UpdateStream(c, Id, change));
    }
}
