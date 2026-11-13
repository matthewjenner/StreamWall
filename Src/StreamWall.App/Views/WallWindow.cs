using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Platform;
using StreamWall.App.ViewModels;
using StreamWall.App.Web;
using StreamWall.Core.Diagnostics;

namespace StreamWall.App.Views;

/// <summary>A wall: a borderless window on its monitor holding the grid of cells.</summary>
public sealed class WallWindow : Window
{
    private readonly WallViewModel _vm;
    private readonly WebEnvironment _web;
    private readonly DiagLog _log;

    public WallWindow(WallViewModel vm, WebEnvironment web, DiagLog log)
    {
        _vm = vm;
        _web = web;
        _log = log;
        WindowDecorations = WindowDecorations.None;
        Background = Brushes.Black;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Title = vm.Title;

        vm.GridChanged += BuildGrid;
        vm.PlacementChanged += Place;
        vm.TitleChanged += () => Title = vm.Title;
        BuildGrid();
        Place();
        Opened += (_, _) => Place();
    }

    public WallViewModel ViewModel => _vm;

    private void BuildGrid()
    {
        var grid = new UniformGrid { Rows = _vm.Rows, Columns = _vm.Columns };
        foreach (CellViewModel cell in _vm.Cells)
            grid.Children.Add(new CellView(_web, _log) { DataContext = cell, Margin = new Thickness(1) });
        Content = grid;
    }

    /// <summary>The cells, for screenshot mode.</summary>
    internal IEnumerable<CellView> CellViews => (Content as Panel)?.Children.OfType<CellView>() ?? [];

    /// <summary>
    /// On the wall's monitor (the primary if that one is gone): fullscreen, or a window centered at
    /// 60% of the monitor (saved positions arrive in M4).
    /// </summary>
    private void Place()
    {
        IReadOnlyList<Screen> all = Screens.All;
        Screen? screen = _vm.Monitor < all.Count ? all[_vm.Monitor] : Screens.Primary ?? all.FirstOrDefault();
        if (screen is null)
            return;

        if (WindowState == WindowState.FullScreen)
            WindowState = WindowState.Normal;
        PixelRect work = screen.WorkingArea;
        PixelRect area = _vm.Fullscreen
            ? screen.Bounds
            : new PixelRect(work.X + work.Width / 5, work.Y + work.Height / 5, work.Width * 3 / 5, work.Height * 3 / 5);
        Position = area.Position;
        Width = area.Width / screen.Scaling;
        Height = area.Height / screen.Scaling;
        if (_vm.Fullscreen && IsVisible)
            WindowState = WindowState.FullScreen;
    }
}
