using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using StreamWall.Core.Config;
using StreamWall.Core.Presentation;

namespace StreamWall.App.Views;

/// <summary>
/// Send to wall: every wall as a small grid; click a cell (a filled one asks before replacing), or
/// take the next empty cell. With no walls yet, it offers to add one. Closes with the chosen cell,
/// or null when canceled. It is its own window because nothing can draw over the Browser's web view.
/// </summary>
public sealed class PickerWindow : Window
{
    private readonly Func<WallMap> _addWall;
    private readonly string _what;
    private readonly CellRef? _swapFrom;
    private readonly StackPanel _root = new() { Margin = new Thickness(20), Spacing = 14 };
    private WallMap _map;

    /// <param name="swapFrom">
    /// Swap mode: the cell whose stream is being swapped. It is marked and not choosable, any other
    /// cell is chosen with one click (no replace confirm: a swap loses nothing), and "next empty"
    /// moves the stream there.
    /// </param>
    public PickerWindow(WallMap map, string what, Func<WallMap> addWall, CellRef? swapFrom = null)
    {
        _map = map;
        _what = what;
        _addWall = addWall;
        _swapFrom = swapFrom;
        Title = swapFrom is null ? "Send to wall" : "Swap";
        Icon = AppIcon.Window;
        SizeToContent = SizeToContent.WidthAndHeight;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        MinWidth = 360;
        Content = _root;
        ShowMap();
    }

    /// <summary>For tests and the UI: pick a cell as if clicked.</summary>
    internal void Choose(WallMapCell cell)
    {
        if (cell.Ref == _swapFrom)
            return;
        if (cell.IsEmpty || _swapFrom is not null)
            Close(cell.Ref);
        else
            ShowConfirm(cell);
    }

    internal WallMap Map => _map;

    private void ShowMap()
    {
        _root.Children.Clear();
        _root.Children.Add(new TextBlock
        {
            Text = _swapFrom is null ? "Send " + _what + " to:" : "Swap " + _what + " with:",
            FontSize = 16,
            FontWeight = FontWeight.SemiBold,
        });

        if (_map.Walls.Count == 0)
        {
            _root.Children.Add(new TextBlock { Text = "There are no walls yet.", Opacity = 0.7 });
            var add = new Button { Content = "Add a 2x2 wall", Name = "AddWallButton" };
            add.Click += (_, _) => { _map = _addWall(); ShowMap(); };
            _root.Children.Add(add);
        }
        else
        {
            var walls = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 24 };
            foreach (WallMapWall wall in _map.Walls)
                walls.Children.Add(new WallMapView(wall, pick: true, Choose, highlight: _swapFrom));
            _root.Children.Add(walls);
        }

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        if (_map.NextEmpty is CellRef next)
        {
            string text = (_swapFrom is null ? "Next empty cell (" : "Move to next empty cell (") + _map.Describe(next) + ")";
            var nextButton = new Button { Content = text, Name = "NextEmptyButton", Classes = { "accent" } };
            nextButton.Click += (_, _) => Close(next);
            buttons.Children.Add(nextButton);
        }
        var cancel = new Button { Content = "Cancel", Name = "CancelButton" };
        cancel.Click += (_, _) => Close(null);
        buttons.Children.Add(cancel);
        _root.Children.Add(buttons);
    }

    private void ShowConfirm(WallMapCell cell)
    {
        _root.Children.Clear();
        _root.Children.Add(new TextBlock
        {
            Text = "Replace \"" + cell.Label + "\" in " + _map.Describe(cell.Ref) + "?",
            FontSize = 16,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 420,
        });
        _root.Children.Add(new TextBlock { Text = "It stays in your stream library.", Opacity = 0.7 });
        var replace = new Button { Content = "Replace", Name = "ReplaceButton", Classes = { "accent" } };
        replace.Click += (_, _) => Close(cell.Ref);
        var back = new Button { Content = "Back", Name = "BackButton" };
        back.Click += (_, _) => ShowMap();
        _root.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Children = { replace, back } });
    }
}
