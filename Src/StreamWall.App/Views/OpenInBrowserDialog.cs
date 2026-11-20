using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using StreamWall.App.ViewModels;

namespace StreamWall.App.Views;

/// <summary>Open in Browser with the setting "Ask each time": keep the cell, or move it off the wall.</summary>
public sealed class OpenInBrowserDialog : Window
{
    public OpenInBrowserDialog(string label)
    {
        Title = "Open in Browser";
        SizeToContent = SizeToContent.WidthAndHeight;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var open = new Button { Content = "Open in Browser", Name = "OpenButton", Classes = { "accent" } };
        var move = new Button { Content = "Move to Browser", Name = "MoveButton" };
        var cancel = new Button { Content = "Cancel", Name = "CancelButton" };
        ToolTip.SetTip(open, "Open it in a new tab; the cell keeps playing.");
        ToolTip.SetTip(move, "Open it in a new tab and clear the cell (it stays in your library).");
        open.Click += (_, _) => Close(OpenInBrowserChoice.Open);
        move.Click += (_, _) => Close(OpenInBrowserChoice.Move);
        cancel.Click += (_, _) => Close(null);

        Content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 14,
            Children =
            {
                new TextBlock { Text = "Open \"" + label + "\" in a Browser tab:", FontSize = 16, FontWeight = FontWeight.SemiBold, MaxWidth = 460, TextWrapping = TextWrapping.Wrap },
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Children = { open, move, cancel } },
            },
        };
    }
}
