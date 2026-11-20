using StreamWall.App.ViewModels;
using StreamWall.App.Views;
using StreamWall.Core.Config;

namespace StreamWall.App.Services;

/// <summary>
/// Keeps one window per open wall in step with the config: opens walls marked open, closes the
/// rest, and updates open walls in place. A wall the user closes (Alt+F4) is marked closed. Cells
/// are staggered: each new web view starts at least <see cref="StartGap"/> after the previous one.
/// </summary>
public sealed class WallManager
{
    public static readonly TimeSpan StartGap = TimeSpan.FromMilliseconds(300);

    private readonly AppHost _host;
    private readonly Dictionary<Guid, WallWindow> _open = [];
    private DateTime _nextStart = DateTime.MinValue;
    private bool _closingAll;

    public WallManager(AppHost host)
    {
        _host = host;
        host.ConfigChanged += Sync;
        host.ReloadAllRequested += ReloadAll;
        if (host.IsLoaded)
            Sync(host.Config);
    }

    public IReadOnlyCollection<WallWindow> OpenWindows => _open.Values;

    /// <summary>Shows the swap picker over a wall (set by the App; tests may fake it).</summary>
    public Func<WallWindow, CellViewModel, Task<CellRef?>>? PickSwapTarget { get; set; }

    /// <summary>Asks "open or move?" over a wall, for Open in Browser set to Ask (set by the App).</summary>
    public Func<WallWindow, CellViewModel, Task<OpenInBrowserChoice?>>? AskOpenInBrowser { get; set; }

    /// <summary>Reloads every cell on every open wall, staggered.</summary>
    public void ReloadAll() => _host.Log.ReloadAll(_open.Values.Sum(w => w.ViewModel.ReloadAll()));

    public void Sync(AppConfig config)
    {
        if (_closingAll)
            return;

        foreach ((WallConfig wall, int index) in config.Walls.Select((w, i) => (w, i)))
        {
            bool isOpen = _open.TryGetValue(wall.Id, out WallWindow? window);
            if (wall.IsOpen && !isOpen)
            {
                WallWindow? owner = null;
                var vm = new WallViewModel(_host, wall.Id, NextStartDelay,
                    cell => owner is not null && PickSwapTarget is { } pick ? pick(owner, cell) : Task.FromResult<CellRef?>(null),
                    cell => owner is not null && AskOpenInBrowser is { } ask ? ask(owner, cell) : Task.FromResult<OpenInBrowserChoice?>(null));
                vm.Apply(config);
                window = new WallWindow(vm, _host.Web, _host.Log);
                owner = window;
                Guid id = wall.Id;
                window.Closed += (_, _) => OnClosedByUser(id);
                _open[wall.Id] = window;
                window.Show();
                _host.Log.WallOpened(index + 1, wall.Rows, wall.Columns);
            }
            else if (wall.IsOpen && isOpen)
            {
                window!.ViewModel.Apply(config);
            }
            else if (!wall.IsOpen && isOpen)
            {
                Close(wall.Id);
                _host.Log.WallClosed(index + 1);
            }
        }

        foreach (Guid gone in _open.Keys.Where(id => config.Walls.All(w => w.Id != id)).ToList())
            Close(gone);
    }

    /// <summary>On exit: close every wall without marking it closed (open walls reopen next launch).</summary>
    public void CloseAll()
    {
        _closingAll = true;
        foreach (Guid id in _open.Keys.ToList())
            Close(id);
    }

    private void Close(Guid id)
    {
        if (_open.Remove(id, out WallWindow? window))
            window.Close();
    }

    private void OnClosedByUser(Guid id)
    {
        if (_closingAll || !_open.Remove(id))
            return;
        int number = _host.Config.Walls.ToList().FindIndex(w => w.Id == id) + 1;
        _host.Update(c => WallLayout.UpdateWall(c, id, w => w with { IsOpen = false }));
        _host.Log.WallClosed(number);
    }

    private TimeSpan NextStartDelay()
    {
        DateTime now = DateTime.UtcNow;
        DateTime start = _nextStart > now ? _nextStart : now;
        _nextStart = start + StartGap;
        return start - now;
    }
}
