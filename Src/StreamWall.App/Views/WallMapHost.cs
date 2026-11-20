using Avalonia;
using Avalonia.Controls;

namespace StreamWall.App.Views;

/// <summary>Hosts a <see cref="WallMapView"/> for a bound wall (the Walls tab's overview).</summary>
public sealed class WallMapHost : ContentControl
{
    public static readonly StyledProperty<StreamWall.Core.Presentation.WallMapWall?> MapProperty =
        AvaloniaProperty.Register<WallMapHost, StreamWall.Core.Presentation.WallMapWall?>(nameof(Map));

    public static readonly StyledProperty<System.Windows.Input.ICommand?> ClearCommandProperty =
        AvaloniaProperty.Register<WallMapHost, System.Windows.Input.ICommand?>(nameof(ClearCommand));

    public StreamWall.Core.Presentation.WallMapWall? Map
    {
        get => GetValue(MapProperty);
        set => SetValue(MapProperty, value);
    }

    public System.Windows.Input.ICommand? ClearCommand
    {
        get => GetValue(ClearCommandProperty);
        set => SetValue(ClearCommandProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == MapProperty)
            Content = Map is { } wall ? new WallMapView(wall, pick: false, _ => { }, cell => ClearCommand?.Execute(cell)) : null;
    }
}
