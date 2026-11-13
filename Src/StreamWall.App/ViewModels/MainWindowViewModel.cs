using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using StreamWall.App.Services;
using StreamWall.Core.Config;

namespace StreamWall.App.ViewModels;

/// <summary>The control panel. M2 is the shell; the stream list, site settings and walls arrive in M3/M4.</summary>
public sealed partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isLoading = true;

    [ObservableProperty]
    private string _status = "Loading...";

    [ObservableProperty]
    private int _streamCount;

    public MainWindowViewModel(AppHost host)
    {
        Title = host.Demo ? "StreamWall (demo)" : "StreamWall";
        Version = "v" + AppHost.Version;
        host.ConfigChanged += Apply;
        if (host.IsLoaded)
            Apply(host.Config);
    }

    public string Title { get; }

    public string Version { get; }

    private void Apply(AppConfig config)
    {
        IsLoading = false;
        StreamCount = config.Streams.Count;
        Status = StreamCount switch
        {
            0 => "No streams yet.",
            1 => "1 stream configured.",
            _ => string.Create(CultureInfo.InvariantCulture, $"{StreamCount} streams configured."),
        };
    }
}
