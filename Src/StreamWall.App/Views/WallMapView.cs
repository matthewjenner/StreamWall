using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using StreamWall.Core.Config;
using StreamWall.Core.Presentation;

namespace StreamWall.App.Views;

/// <summary>
/// One wall drawn as a small grid of buttons (labels only). In the picker every cell is clickable;
/// in the Walls tab filled cells offer a clear button instead.
/// </summary>
public sealed class WallMapView : StackPanel
{
    public WallMapView(WallMapWall wall, bool pick, Action<WallMapCell> chosen, Action<WallMapCell>? clear = null, CellRef? highlight = null)
    {
        Spacing = 4;
        Children.Add(new TextBlock
        {
            Text = wall.IsOpen ? wall.Name : wall.Name + " (closed)",
            FontWeight = FontWeight.SemiBold,
        });

        var grid = new UniformGrid { Rows = wall.Rows, Columns = wall.Columns, Width = 72 * wall.Columns, Height = 46 * wall.Rows };
        foreach (WallMapCell cell in wall.Cells)
        {
            var text = new TextBlock
            {
                Text = cell.Number + "  " + cell.Label,
                TextWrapping = TextWrapping.Wrap,
                TextTrimming = TextTrimming.CharacterEllipsis,
                FontSize = 11,
                Opacity = cell.IsEmpty ? 0.55 : 1,
                // Room for the clear button in the corner.
                Margin = !pick && !cell.IsEmpty ? new Thickness(0, 12, 0, 0) : default,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Control content = text;
            if (!pick && !cell.IsEmpty && clear is not null)
            {
                var x = new Button { Content = "X", FontSize = 10, Padding = new Thickness(4, 0), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Name = "ClearCell" };
                ToolTip.SetTip(x, "Clear this cell");
                x.Click += (_, e) => { e.Handled = true; clear(cell); };
                content = new Panel { Children = { text, x } };
            }
            var button = new Button
            {
                Content = content,
                Margin = new Thickness(2),
                Padding = new Thickness(2),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Stretch,
                IsHitTestVisible = pick || !cell.IsEmpty,
                // The cell being swapped: marked, not choosable.
                IsEnabled = cell.Ref != highlight,
                BorderBrush = cell.Ref == highlight ? Brushes.DodgerBlue : null,
                BorderThickness = cell.Ref == highlight ? new Thickness(2) : default,
                Tag = cell,
            };
            if (pick)
                button.Click += (_, _) => chosen(cell);
            grid.Children.Add(button);
        }
        Children.Add(grid);
    }
}
