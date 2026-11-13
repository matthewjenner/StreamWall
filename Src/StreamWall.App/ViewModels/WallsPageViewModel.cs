using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StreamWall.App.Services;
using StreamWall.Core.Config;
using StreamWall.Core.Presentation;

namespace StreamWall.App.ViewModels;

/// <summary>The Walls tab: the 1-3 walls (grid, monitor, open), a map of each, master audio.</summary>
public sealed partial class WallsPageViewModel : ObservableObject
{
    private readonly AppHost _host;
    private bool _applying;

    public WallsPageViewModel(AppHost host)
    {
        _host = host;
        host.ConfigChanged += Apply;
        if (host.IsLoaded)
            Apply(host.Config);
    }

    public ObservableCollection<WallRowViewModel> Walls { get; } = [];

    public static IReadOnlyList<int> GridSizes { get; } = [1, 2, 3, 4];

    /// <summary>"1: 2560 x 1440 (primary)" per monitor; set by the window, which knows the screens.</summary>
    [ObservableProperty]
    private IReadOnlyList<string> _monitors = ["Monitor 1"];

    partial void OnMonitorsChanged(IReadOnlyList<string> value)
    {
        foreach (WallRowViewModel row in Walls)
            row.Monitors = value;
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddWallCommand))]
    private bool _canAddWall = true;

    [ObservableProperty]
    private int _masterVolume = 100;

    [ObservableProperty]
    private bool _masterMuted;

    [ObservableProperty]
    private bool _hasNoWalls = true;

    [RelayCommand(CanExecute = nameof(CanAddWall))]
    private void AddWall() => _host.Update(c => WallLayout.AddWall(c).Config);

    partial void OnMasterVolumeChanged(int value)
    {
        if (!_applying)
            _host.Update(c => c with { MasterVolume = value });
    }

    partial void OnMasterMutedChanged(bool value)
    {
        if (!_applying)
            _host.Update(c => c with { MasterMuted = value });
    }

    private void Apply(AppConfig config)
    {
        _applying = true;
        try
        {
            MasterVolume = config.MasterVolume;
            MasterMuted = config.MasterMuted;
            CanAddWall = WallLayout.CanAddWall(config);
            HasNoWalls = config.Walls.Count == 0;

            WallMap map = WallMap.From(config);
            for (int i = Walls.Count - 1; i >= 0; i--)
            {
                if (config.Walls.All(w => w.Id != Walls[i].Id))
                    Walls.RemoveAt(i);
            }
            for (int i = 0; i < config.Walls.Count; i++)
            {
                WallConfig wall = config.Walls[i];
                WallRowViewModel? row = Walls.FirstOrDefault(r => r.Id == wall.Id);
                if (row is null)
                {
                    row = new WallRowViewModel(_host, wall.Id) { Monitors = Monitors };
                    Walls.Insert(Math.Min(i, Walls.Count), row);
                }
                row.Apply(wall, map.Walls[i]);
            }
        }
        finally
        {
            _applying = false;
        }
    }
}

/// <summary>One wall's settings row. Edits go straight to the config.</summary>
public sealed partial class WallRowViewModel : ObservableObject
{
    private readonly AppHost _host;
    private bool _applying;

    public WallRowViewModel(AppHost host, Guid id)
    {
        _host = host;
        Id = id;
    }

    public Guid Id { get; }

    [ObservableProperty]
    private string _name = "";

    [ObservableProperty]
    private int _rows = 2;

    [ObservableProperty]
    private int _columns = 2;

    [ObservableProperty]
    private int _monitor;

    [ObservableProperty]
    private bool _isOpen = true;

    [ObservableProperty]
    private bool _fullscreen = true;

    [ObservableProperty]
    private WallMapWall? _map;

    [ObservableProperty]
    private IReadOnlyList<string> _monitors = ["Monitor 1"];

    private int _configMonitor;

    /// <summary>
    /// A new monitor list makes the ComboBox drop its selection; put the configured monitor back
    /// (without writing anything).
    /// </summary>
    partial void OnMonitorsChanged(IReadOnlyList<string> value) =>
        // After the ComboBox has the new items (this runs before it is told about them).
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            _applying = true;
            try
            {
                Monitor = -1;
                Monitor = _configMonitor;
            }
            finally
            {
                _applying = false;
            }
        });

    public void Apply(WallConfig wall, WallMapWall map)
    {
        _applying = true;
        try
        {
            Name = wall.Name;
            Rows = wall.Rows;
            Columns = wall.Columns;
            _configMonitor = wall.Monitor;
            Monitor = wall.Monitor;
            IsOpen = wall.IsOpen;
            Fullscreen = wall.Fullscreen;
            Map = map;
        }
        finally
        {
            _applying = false;
        }
    }

    [RelayCommand]
    private void Remove() => _host.Update(c => WallLayout.RemoveWall(c, Id));

    [RelayCommand]
    private void ClearCell(WallMapCell cell)
    {
        _host.Update(c => WallLayout.Clear(c, cell.Ref));
        _host.Log.CellCleared(_host.WallNumber(Id), cell.Number);
    }

    partial void OnNameChanged(string value) => Change(w => w with { Name = value });

    partial void OnRowsChanged(int value)
    {
        if (!_applying)
            _host.Update(c => WallLayout.Resize(c, Id, value, Columns));
    }

    partial void OnColumnsChanged(int value)
    {
        if (!_applying)
            _host.Update(c => WallLayout.Resize(c, Id, Rows, value));
    }

    partial void OnMonitorChanged(int value)
    {
        // -1 is the ComboBox briefly having no selection (its items were replaced), not a choice.
        if (value >= 0)
            Change(w => w with { Monitor = value });
    }

    partial void OnIsOpenChanged(bool value) => Change(w => w with { IsOpen = value });

    partial void OnFullscreenChanged(bool value) => Change(w => w with { Fullscreen = value });

    private void Change(Func<WallConfig, WallConfig> change)
    {
        if (!_applying)
            _host.Update(c => WallLayout.UpdateWall(c, Id, change));
    }
}
