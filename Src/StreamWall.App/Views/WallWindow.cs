using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using StreamWall.App.ViewModels;
using StreamWall.App.Web;
using StreamWall.Core.Config;
using StreamWall.Core.Diagnostics;

namespace StreamWall.App.Views;

/// <summary>
/// A wall: fullscreen and borderless on its monitor, or a normal window (title bar, movable,
/// resizable) that remembers where the user put it. Walls open without taking focus, so the
/// control panel stays in front.
/// </summary>
public sealed class WallWindow : Window
{
    private readonly WallViewModel _vm;
    private readonly WebEnvironment _web;
    private readonly DiagLog _log;
    private static readonly TimeSpan SaveBoundsAfter = TimeSpan.FromMilliseconds(600);
    private int _moveGeneration;
    private bool _placing;

    public WallWindow(WallViewModel vm, WebEnvironment web, DiagLog log)
    {
        _vm = vm;
        _web = web;
        _log = log;
        Background = Brushes.Black;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Icon = AppIcon.Window;
        ShowActivated = false;
        Title = vm.Title;

        vm.GridChanged += BuildGrid;
        vm.PlacementChanged += Place;
        vm.TitleChanged += () => Title = vm.Title;
        BuildGrid();
        Place();
        Opened += (_, _) => Place();

        // The user moving or resizing a windowed wall: remember it once they stop.
        PositionChanged += (_, _) => UserMovedOrResized();
        SizeChanged += (_, _) => UserMovedOrResized();
    }

    public WallViewModel ViewModel => _vm;

    /// <summary>The cells, for screenshot mode.</summary>
    internal IEnumerable<CellView> CellViews => (Content as Panel)?.Children.OfType<CellView>() ?? [];

    private void BuildGrid()
    {
        var grid = new UniformGrid { Rows = _vm.Rows, Columns = _vm.Columns };
        foreach (CellViewModel cell in _vm.Cells)
            grid.Children.Add(new CellView(_web, _log) { DataContext = cell, Margin = new Thickness(1) });
        Content = grid;
    }

    /// <summary>
    /// Fullscreen: borderless over the whole monitor. Window: a normal window at its saved bounds if
    /// they are still on a screen, else centered at 60% of its monitor (the primary if it is gone).
    /// </summary>
    private void Place()
    {
        _placing = true;
        try
        {
            IReadOnlyList<Screen> all = Screens.All;
            Screen? screen = _vm.Monitor < all.Count ? all[_vm.Monitor] : Screens.Primary ?? all.FirstOrDefault();
            if (screen is null)
                return;

            if (WindowState is WindowState.FullScreen or WindowState.Maximized)
                WindowState = WindowState.Normal;

            if (_vm.Fullscreen)
            {
                WindowDecorations = WindowDecorations.None;
                CanResize = false;
                Position = screen.Bounds.Position;
                Width = screen.Bounds.Width / screen.Scaling;
                Height = screen.Bounds.Height / screen.Scaling;
                if (IsVisible)
                    WindowState = WindowState.FullScreen;
                return;
            }

            WindowDecorations = WindowDecorations.Full;
            CanResize = true;
            if (_vm.Bounds is { } saved && all.Any(s => s.Bounds.Contains(new PixelPoint(saved.X + 40, saved.Y + 20))))
            {
                Position = new PixelPoint(saved.X, saved.Y);
                Width = saved.Width;
                Height = saved.Height;
            }
            else
            {
                PixelRect work = screen.WorkingArea;
                Position = new PixelPoint(work.X + work.Width / 5, work.Y + work.Height / 5);
                Width = work.Width * 3 / 5 / screen.Scaling;
                Height = work.Height * 3 / 5 / screen.Scaling;
            }
        }
        finally
        {
            // Let the window settle before move/resize events count as the user's.
            Dispatcher.UIThread.Post(() => _placing = false, DispatcherPriority.Background);
        }
    }

    private void UserMovedOrResized()
    {
        if (_placing || _vm.Fullscreen || WindowState != WindowState.Normal || !IsVisible)
            return;
        _ = SaveBoundsLaterAsync(++_moveGeneration);
    }

    /// <summary>Saves once the user has stopped moving or resizing for a moment (one write per drag).</summary>
    private async Task SaveBoundsLaterAsync(int generation)
    {
        await Task.Delay(SaveBoundsAfter);
        if (generation != _moveGeneration || _placing || _vm.Fullscreen || WindowState != WindowState.Normal)
            return;
        _vm.SaveBounds(new WallBounds(Position.X, Position.Y, Math.Round(ClientSize.Width), Math.Round(ClientSize.Height)));
    }
}
