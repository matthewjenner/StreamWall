using System.Globalization;
using Avalonia.Controls;
using Avalonia.Platform;
using StreamWall.App.ViewModels;
using StreamWall.Core.Config;
using StreamWall.Core.Presentation;

namespace StreamWall.App.Views;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Opened += (_, _) => OnOpened();
    }

    /// <summary>The picker source; set by the App (tests leave it null and fake PickCell).</summary>
    public Func<WallMap>? CurrentMap { get; set; }

    public Func<WallMap>? AddWall { get; set; }

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
