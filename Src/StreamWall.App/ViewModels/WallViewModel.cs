using System.Collections.ObjectModel;
using StreamWall.App.Services;
using StreamWall.Core.Config;

namespace StreamWall.App.ViewModels;

/// <summary>
/// One open wall: its grid and cells. <see cref="Apply"/> updates cells in place, so only a cell whose
/// stream changed reloads; a new grid size rebuilds the cells (<see cref="GridChanged"/>).
/// </summary>
public sealed class WallViewModel
{
    private readonly AppHost _host;
    private readonly Func<TimeSpan> _nextStartDelay;

    public WallViewModel(AppHost host, Guid wallId, Func<TimeSpan> nextStartDelay)
    {
        _host = host;
        Id = wallId;
        _nextStartDelay = nextStartDelay;
    }

    public Guid Id { get; }

    /// <summary>1-based position in the config; what logs use (names are the user's).</summary>
    public int Number { get; private set; }

    public string Name { get; private set; } = "";

    public int Rows { get; private set; }

    public int Columns { get; private set; }

    public int Monitor { get; private set; }

    public bool Fullscreen { get; private set; }

    public string Title => "StreamWall - " + Name;

    public ObservableCollection<CellViewModel> Cells { get; } = [];

    /// <summary>Raised after the grid size changed and <see cref="Cells"/> was rebuilt.</summary>
    public event Action? GridChanged;

    /// <summary>Raised when the wall's monitor or fullscreen setting changed.</summary>
    public event Action? PlacementChanged;

    public event Action? TitleChanged;

    public void Apply(AppConfig config)
    {
        int index = -1;
        for (int i = 0; i < config.Walls.Count; i++)
        {
            if (config.Walls[i].Id == Id)
                index = i;
        }
        if (index < 0)
            return;
        WallConfig wall = config.Walls[index];
        Number = index + 1;

        bool placement = wall.Monitor != Monitor || wall.Fullscreen != Fullscreen;
        bool title = wall.Name != Name;
        bool grid = wall.Rows != Rows || wall.Columns != Columns || Cells.Count != wall.CellCount;
        Name = wall.Name;
        Monitor = wall.Monitor;
        Fullscreen = wall.Fullscreen;
        Rows = wall.Rows;
        Columns = wall.Columns;

        if (grid)
        {
            Cells.Clear();
            for (int i = 0; i < wall.CellCount; i++)
                Cells.Add(new CellViewModel(new CellRef(Id, i), Number, ClearCell, SetVolume, SetMuted) { StartDelay = _nextStartDelay() });
        }
        for (int i = 0; i < Cells.Count; i++)
        {
            Cells[i].WallNumber = Number;
            Cells[i].Apply(config, wall.Cells[i]);
        }

        if (grid)
            GridChanged?.Invoke();
        if (placement)
            PlacementChanged?.Invoke();
        if (title)
            TitleChanged?.Invoke();
    }

    private void ClearCell(CellViewModel cell)
    {
        _host.Update(c => WallLayout.Clear(c, cell.Ref));
        _host.Log.CellCleared(Number, cell.Number);
    }

    private void SetVolume(Guid stream, int volume) => _host.Update(c => WallLayout.UpdateStream(c, stream, s => s with { Volume = volume }));

    private void SetMuted(Guid stream, bool muted) => _host.Update(c => WallLayout.UpdateStream(c, stream, s => s with { Muted = muted }));
}
