using System.Collections.ObjectModel;
using StreamWall.App.Services;
using StreamWall.Core.Config;

namespace StreamWall.App.ViewModels;

/// <summary>
/// One open wall: its grid and cells. <see cref="Apply"/> updates cells in place, so only a cell whose
/// stream changed reloads; a new grid size rebuilds the cells (<see cref="GridChanged"/>).
/// </summary>
public sealed class WallViewModel : ICellActions
{
    private readonly AppHost _host;
    private readonly Func<TimeSpan> _nextStartDelay;
    private readonly Func<CellViewModel, Task<CellRef?>> _pickSwapTarget;
    private readonly Func<CellViewModel, Task<OpenInBrowserChoice?>> _askOpenInBrowser;

    public WallViewModel(AppHost host, Guid wallId, Func<TimeSpan> nextStartDelay, Func<CellViewModel, Task<CellRef?>>? pickSwapTarget = null,
        Func<CellViewModel, Task<OpenInBrowserChoice?>>? askOpenInBrowser = null)
    {
        _host = host;
        Id = wallId;
        _nextStartDelay = nextStartDelay;
        _pickSwapTarget = pickSwapTarget ?? (_ => Task.FromResult<CellRef?>(null));
        _askOpenInBrowser = askOpenInBrowser ?? (_ => Task.FromResult<OpenInBrowserChoice?>(null));
    }

    public Guid Id { get; }

    /// <summary>1-based position in the config; what logs use (names are the user's).</summary>
    public int Number { get; private set; }

    public string Name { get; private set; } = "";

    public int Rows { get; private set; }

    public int Columns { get; private set; }

    public int Monitor { get; private set; }

    public bool Fullscreen { get; private set; }

    /// <summary>Where the user last put this wall as a window; null centers it on its monitor.</summary>
    public WallBounds? Bounds { get; private set; }

    /// <summary>The window was moved or resized by the user (windowed walls only).</summary>
    public void SaveBounds(WallBounds bounds)
    {
        if (bounds.IsUsable && bounds != Bounds)
        {
            Bounds = bounds;
            _host.Update(c => WallLayout.UpdateWall(c, Id, w => w with { Bounds = bounds }));
        }
    }

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

        // A bounds change from the window itself is not a reason to re-place it; one that comes
        // with a monitor or mode change is (and a cleared one recenters).
        bool placement = wall.Monitor != Monitor || wall.Fullscreen != Fullscreen || (wall.Bounds is null && Bounds is not null);
        Bounds = wall.Bounds;
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
                Cells.Add(new CellViewModel(new CellRef(Id, i), Number, this) { StartDelay = _nextStartDelay() });
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

    /// <summary>Reload all: every cell with a stream, staggered like startup.</summary>
    public int ReloadAll()
    {
        int count = 0;
        foreach (CellViewModel cell in Cells.Where(c => !c.IsEmpty))
        {
            cell.RequestReload(_nextStartDelay());
            count++;
        }
        return count;
    }

    // ICellActions: what a cell's strip asks for.

    public void Clear(CellViewModel cell)
    {
        _host.Update(c => WallLayout.Clear(c, cell.Ref));
        _host.Log.CellCleared(Number, cell.Number);
    }

    public void SetVolume(Guid stream, int volume) => _host.Update(c => WallLayout.UpdateStream(c, stream, s => s with { Volume = volume }));

    public void SetMuted(Guid stream, bool muted) => _host.Update(c => WallLayout.UpdateStream(c, stream, s => s with { Muted = muted }));

    public void Reloaded(CellViewModel cell) => _host.Log.CellReloaded(Number, cell.Number);

    public async Task SwapAsync(CellViewModel cell)
    {
        if (await _pickSwapTarget(cell) is not CellRef target || target == cell.Ref)
            return;
        _host.Update(c => WallLayout.Swap(c, cell.Ref, target));
        _host.Log.CellsSwapped(Number, cell.Number, _host.WallNumber(target.WallId), target.Index + 1);
    }

    public void PageTitle(Guid stream, string? title) => _host.Update(c => StreamLabels.ApplyPageTitle(c, stream, title));

    /// <summary>
    /// The cell's page in a new Browser tab. Settings decide the cell: keep it playing, move it off
    /// the wall (it stays in the library), or ask each time.
    /// </summary>
    public async Task OpenInBrowserAsync(CellViewModel cell)
    {
        if (cell.Url is not { } url)
            return;
        bool move;
        switch (_host.Config.OpenInBrowser)
        {
            case OpenInBrowserMode.MoveOffWall:
                move = true;
                break;
            case OpenInBrowserMode.Ask:
                if (await _askOpenInBrowser(cell) is not OpenInBrowserChoice choice)
                    return;
                move = choice == OpenInBrowserChoice.Move;
                break;
            default:
                move = false;
                break;
        }
        _host.OpenInBrowser(url);
        if (move)
            _host.Update(c => WallLayout.Clear(c, cell.Ref));
        _host.Log.CellOpenedInBrowser(Number, cell.Number, move);
    }
}

/// <summary>The answer to "Open in Browser" when the setting is Ask each time.</summary>
public enum OpenInBrowserChoice
{
    /// <summary>Open it in a tab and keep it on the wall.</summary>
    Open,

    /// <summary>Open it in a tab and clear the cell.</summary>
    Move,
}
