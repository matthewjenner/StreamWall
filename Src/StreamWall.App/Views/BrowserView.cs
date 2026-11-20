using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using StreamWall.App.ViewModels;

namespace StreamWall.App.Views;

/// <summary>
/// The Browser tab: a tab strip (title, audio mute, close; middle-click closes; + opens), a toolbar
/// (back, forward, reload, address, zoom, Send to wall) and the tabs' web views, of which only the
/// selected one shows. Background tabs keep running (some sites pause hidden video).
/// </summary>
public sealed class BrowserView : DockPanel
{
    private static readonly IBrush SelectedTabBrush = new SolidColorBrush(Color.FromRgb(0x2B, 0x31, 0x3A));
    private static readonly IBrush TabBrush = new SolidColorBrush(Color.FromRgb(0x17, 0x1B, 0x21));

    private readonly StackPanel _tabStrip = new() { Orientation = Orientation.Horizontal, Spacing = 2 };
    private readonly Button _newTab = Icons.Button(Icons.Add, "New tab", "NewTabButton");
    private readonly Button _back = Icons.Button(Icons.Back, "Back", "BackButton");
    private readonly Button _forward = Icons.Button(Icons.Forward, "Forward", "ForwardButton");
    private readonly Button _reload = Icons.Button(Icons.Reload, "Reload", "ReloadButton");
    private readonly TextBox _address = new() { Name = "AddressBox", PlaceholderText = "Address", VerticalContentAlignment = VerticalAlignment.Center };
    private readonly Button _zoomOut = Icons.Button(Icons.ZoomOut, "Zoom out", "ZoomOutButton");
    private readonly Button _zoomText = new() { Name = "ZoomResetButton", Padding = new Thickness(6, 0), Height = 26, MinWidth = 52, Background = Brushes.Transparent };
    private readonly Button _zoomIn = Icons.Button(Icons.ZoomIn, "Zoom in", "ZoomInButton");
    private readonly Button _send = new() { Content = "Send to wall", Name = "SendToWallButton", Classes = { "accent" } };
    private readonly TextBlock _message = new() { Opacity = 0.75, Margin = new Thickness(0, 4, 0, 4), TextWrapping = TextWrapping.Wrap };
    private readonly Panel _body = new() { Background = Brushes.White };
    private readonly Dictionary<Guid, BrowserTabView> _views = [];

    private BrowserViewModel? _vm;
    private BrowserTabViewModel? _selected;

    public BrowserView()
    {
        ToolTip.SetTip(_zoomText, "Reset zoom to 100%");

        var strip = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
        DockPanel.SetDock(_newTab, Dock.Right);
        strip.Children.Add(_newTab);
        strip.Children.Add(new ScrollViewer
        {
            Content = _tabStrip,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
        });

        var toolbar = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
        var left = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, Margin = new Thickness(0, 0, 6, 0) };
        left.Children.Add(_back);
        left.Children.Add(_forward);
        left.Children.Add(_reload);
        var right = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, Margin = new Thickness(6, 0, 0, 0) };
        right.Children.Add(_zoomOut);
        right.Children.Add(_zoomText);
        right.Children.Add(_zoomIn);
        right.Children.Add(new Border { Width = 8 });
        right.Children.Add(_send);
        DockPanel.SetDock(left, Dock.Left);
        DockPanel.SetDock(right, Dock.Right);
        toolbar.Children.Add(left);
        toolbar.Children.Add(right);
        toolbar.Children.Add(_address);

        DockPanel.SetDock(strip, Dock.Top);
        DockPanel.SetDock(toolbar, Dock.Top);
        DockPanel.SetDock(_message, Dock.Top);
        Children.Add(strip);
        Children.Add(toolbar);
        Children.Add(_message);
        Children.Add(_body);

        _back.Click += (_, _) => Current?.GoBack();
        _forward.Click += (_, _) => Current?.GoForward();
        _reload.Click += (_, _) => Current?.Reload();
        _address.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter)
                return;
            e.Handled = true;
            string? target = BrowserViewModel.ToAddress(_address.Text ?? "");
            if (target is null && _vm is not null)
                _vm.Message = "That is not an address.";
            else if (target is not null)
                Current?.Navigate(target);
        };
    }

    private BrowserTabView? Current => _selected is not null && _views.TryGetValue(_selected.Id, out BrowserTabView? v) ? v : null;

    /// <summary>Screenshot mode: the selected tab's zoom straight from its controller.</summary>
    internal double? CurrentZoomFactor => Current?.ZoomFactor;

    /// <summary>Screenshot mode: the selected tab's page as PNG.</summary>
    internal Task<bool> CaptureAsync(Stream png) => Current?.CaptureAsync(png) ?? Task.FromResult(false);

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_vm is not null)
        {
            _vm.PropertyChanged -= OnBrowserChanged;
            _vm.Tabs.CollectionChanged -= OnTabsChanged;
        }
        _vm = DataContext as BrowserViewModel;
        if (_vm is null)
            return;
        _vm.PropertyChanged += OnBrowserChanged;
        _vm.Tabs.CollectionChanged += OnTabsChanged;
        _newTab.Command = _vm.NewTabCommand;
        _send.Command = _vm.SendToWallCommand;
        _zoomOut.Command = _vm.ZoomOutCommand;
        _zoomIn.Command = _vm.ZoomInCommand;
        _zoomText.Command = _vm.ZoomResetCommand;
        SyncTabs();
        Select(_vm.SelectedTab);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsVisibleProperty && IsVisible)
            ShowSelected();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (IsEffectivelyVisible)
            ShowSelected();
    }

    private void OnBrowserChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(BrowserViewModel.SelectedTab))
            Select(_vm?.SelectedTab);
        else if (e.PropertyName == nameof(BrowserViewModel.Message))
            _message.Text = _vm?.Message;
    }

    private void OnTabsChanged(object? sender, NotifyCollectionChangedEventArgs e) => SyncTabs();

    /// <summary>One view per tab: new tabs get one (a link's tab starts loading now), closed tabs release theirs.</summary>
    private void SyncTabs()
    {
        if (_vm is null)
            return;
        foreach (Guid gone in _views.Keys.Where(id => _vm.Tabs.All(t => t.Id != id)).ToList())
        {
            _views[gone].Close();
            _body.Children.Remove(_views[gone]);
            _views.Remove(gone);
        }
        foreach (BrowserTabViewModel tab in _vm.Tabs.Where(t => !_views.ContainsKey(t.Id)))
        {
            var view = new BrowserTabView(tab, _vm) { IsVisible = false };
            _views[tab.Id] = view;
            _body.Children.Add(view);
            tab.PropertyChanged += (_, _) => RenderStrip();
            if (tab.LoadNow && IsEffectivelyVisible)
                view.EnsureCreated();
        }
        RenderStrip();
    }

    private void Select(BrowserTabViewModel? tab)
    {
        if (_selected is not null)
            _selected.PropertyChanged -= OnSelectedChanged;
        _selected = tab;
        if (_selected is not null)
            _selected.PropertyChanged += OnSelectedChanged;
        foreach (BrowserTabView view in _views.Values)
            view.IsVisible = view.Tab == tab;
        ShowSelected();
        SyncToolbar();
        RenderStrip();
    }

    private void ShowSelected()
    {
        if (IsEffectivelyVisible)
            Current?.EnsureCreated();
    }

    private void OnSelectedChanged(object? sender, PropertyChangedEventArgs e) => SyncToolbar();

    private void SyncToolbar()
    {
        _back.IsEnabled = _selected?.CanGoBack == true;
        _forward.IsEnabled = _selected?.CanGoForward == true;
        _zoomText.Content = string.Create(CultureInfo.InvariantCulture, $"{_selected?.ZoomPercent ?? 100}%");
        if (!_address.IsFocused)
            _address.Text = _selected?.Url;
    }

    /// <summary>The tab strip: one header per tab (title, audio, close).</summary>
    private void RenderStrip()
    {
        if (_vm is null)
            return;
        _tabStrip.Children.Clear();
        foreach (BrowserTabViewModel tab in _vm.Tabs)
            _tabStrip.Children.Add(Header(tab));
    }

    private Control Header(BrowserTabViewModel tab)
    {
        BrowserViewModel vm = _vm!;
        var title = new TextBlock
        {
            Text = tab.Title,
            MaxWidth = 180,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0),
        };
        var audio = Icons.Button(tab.IsMuted ? Icons.Mute : Icons.Volume, tab.IsMuted ? "Unmute tab" : "Mute tab", "TabAudioButton");
        audio.IsVisible = tab.IsPlayingAudio || tab.IsMuted;
        audio.Command = tab.ToggleMuteCommand;
        var close = Icons.Button(Icons.Close, "Close tab", "TabCloseButton");
        close.Command = vm.CloseTabCommand;
        close.CommandParameter = tab;

        var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(audio);
        row.Children.Add(title);
        row.Children.Add(close);
        var header = new Border
        {
            Name = "TabHeader",
            Tag = tab,
            Child = row,
            Padding = new Thickness(6, 2),
            CornerRadius = new CornerRadius(6, 6, 0, 0),
            Background = tab.IsSelected ? SelectedTabBrush : TabBrush,
            Cursor = new Cursor(StandardCursorType.Hand),
        };
        ToolTip.SetTip(header, tab.Title);
        header.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(header).Properties.IsLeftButtonPressed)
                vm.SelectTabCommand.Execute(tab);
        };
        // Middle-click closes, like a browser.
        header.PointerReleased += (_, e) =>
        {
            if (e.InitialPressMouseButton == MouseButton.Middle)
            {
                e.Handled = true;
                vm.CloseTabCommand.Execute(tab);
            }
        };
        return header;
    }
}
