using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;
using StreamWall.App.Services;
using StreamWall.App.ViewModels;
using StreamWall.Core.Config;
using StreamWall.Core.Presentation;

namespace StreamWall.App.Views;

/// <summary>
/// The control panel. It reopens where it was (Settings, <see cref="WindowPlacementRules"/>):
/// position, size, monitor and maximized are saved shortly after every move or resize and on close;
/// minimized is never saved; an unreachable or ill-fitting placement is corrected on restore.
/// </summary>
public sealed partial class MainWindow : Window
{
    private static readonly TimeSpan SaveAfter = TimeSpan.FromMilliseconds(600);

    private bool _placing;
    private int _moveGeneration;
    private WindowPlacement? _lastNormal;

    public MainWindow()
    {
        InitializeComponent();
        Icon = AppIcon.Window;
        Opened += (_, _) => OnOpened();
        PositionChanged += (_, _) => Moved();
        SizeChanged += (_, _) => Moved();
        // On close, before the shutdown handler saves the config.
        Closing += (_, _) => SavePlacement();
    }

    /// <summary>The picker source; set by the App (tests leave it null and fake PickCell).</summary>
    public Func<WallMap>? CurrentMap { get; set; }

    public Func<WallMap>? AddWall { get; set; }

    /// <summary>Where the placement is read and saved; null (some tests) means it is not remembered.</summary>
    public AppHost? Host { get; set; }

    /// <summary>Puts the window back where it was, once the config is loaded.</summary>
    public void RestorePlacement()
    {
        if (Host is not { Config.RememberControlPanel: true } host)
            return;
        WindowPlacement? placement = WindowPlacementRules.Restore(host.Config.ControlPanel, ScreenAreas(), MinWidth, MinHeight);
        if (placement is null)
            return;

        _placing = true;
        try
        {
            if (WindowState != WindowState.Normal)
                WindowState = WindowState.Normal;
            Position = new PixelPoint(placement.X, placement.Y);
            Width = placement.Width;
            Height = placement.Height;
            _lastNormal = placement with { Maximized = false };
            if (placement.Maximized)
                WindowState = WindowState.Maximized;
        }
        finally
        {
            Dispatcher.UIThread.Post(() => _placing = false, DispatcherPriority.Background);
        }
    }

    /// <summary>Saves where the window is now: normal bounds, or maximized (keeping the last normal bounds). Minimized is ignored.</summary>
    public void SavePlacement()
    {
        if (Host is not { Config.RememberControlPanel: true } host || _placing)
            return;
        WindowPlacement? placement = WindowState switch
        {
            WindowState.Normal => _lastNormal = new WindowPlacement(Position.X, Position.Y, Math.Round(ClientSize.Width), Math.Round(ClientSize.Height)),
            WindowState.Maximized when _lastNormal is not null => _lastNormal with { Maximized = true },
            _ => null, // minimized (or maximized before any normal bounds are known): keep what is saved
        };
        if (placement is not null)
            host.Update(c => c.ControlPanel == placement ? c : c with { ControlPanel = placement });
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == WindowStateProperty)
            Moved();
    }

    private void Moved()
    {
        if (_placing || !IsVisible)
            return;
        _ = SaveLaterAsync(++_moveGeneration);
    }

    /// <summary>Once the user stops moving or resizing for a moment (one save per drag).</summary>
    private async Task SaveLaterAsync(int generation)
    {
        await Task.Delay(SaveAfter);
        if (generation == _moveGeneration)
            SavePlacement();
    }

    private IReadOnlyList<ScreenArea> ScreenAreas() =>
        [.. Screens.All.Select(s => new ScreenArea(s.WorkingArea.X, s.WorkingArea.Y, s.WorkingArea.Width, s.WorkingArea.Height, s.Scaling, s.IsPrimary))];

    private void OnOpened()
    {
        if (DataContext is not MainWindowViewModel vm)
            return;
        vm.Walls.Monitors = [.. Screens.All.Select(Describe)];
        if (CurrentMap is { } map && AddWall is { } add)
            vm.PickCell = what => new PickerWindow(map(), what, add).ShowDialog<CellRef?>(this);
    }

    private string Describe(Screen screen, int index) => string.Create(CultureInfo.InvariantCulture,
        $"Monitor {index + 1}: {screen.Bounds.Width} x {screen.Bounds.Height}{(screen.IsPrimary ? " (primary)" : "")}");
}
