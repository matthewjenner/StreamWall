using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Microsoft.Web.WebView2.Core;
using StreamWall.App.ViewModels;

namespace StreamWall.App.Views;

/// <summary>
/// The Browser tab: an interactive web view on the shared profile with back / forward / reload,
/// an address bar and Send to wall. It reopens at its last page. Links that would open a new
/// window open here instead. The web view is created the first time the tab is shown.
/// </summary>
public sealed class BrowserView : DockPanel
{
    private readonly Button _back = new() { Content = "<", Name = "BackButton" };
    private readonly Button _forward = new() { Content = ">", Name = "ForwardButton" };
    private readonly Button _reload = new() { Content = "Reload", Name = "ReloadButton" };
    private readonly TextBox _address = new() { Name = "AddressBox", PlaceholderText = "Address", VerticalContentAlignment = VerticalAlignment.Center };
    private readonly Button _send = new() { Content = "Send to wall", Name = "SendToWallButton", Classes = { "accent" } };
    private readonly TextBlock _message = new() { Opacity = 0.75, Margin = new Thickness(0, 4, 0, 4), TextWrapping = TextWrapping.Wrap };
    private readonly Panel _body = new() { Background = Brushes.White };

    private BrowserViewModel? _vm;
    private NativeWebView? _view;
    private CoreWebView2? _core;

    public BrowserView()
    {
        var toolbar = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
        var left = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Margin = new Thickness(0, 0, 6, 0) };
        left.Children.Add(_back);
        left.Children.Add(_forward);
        left.Children.Add(_reload);
        var right = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(6, 0, 0, 0) };
        right.Children.Add(_send);
        DockPanel.SetDock(left, Dock.Left);
        DockPanel.SetDock(right, Dock.Right);
        toolbar.Children.Add(left);
        toolbar.Children.Add(right);
        toolbar.Children.Add(_address);
        DockPanel.SetDock(toolbar, Dock.Top);
        DockPanel.SetDock(_message, Dock.Top);
        Children.Add(toolbar);
        Children.Add(_message);
        Children.Add(_body);

        _back.IsEnabled = _forward.IsEnabled = false;
        _back.Click += (_, _) => { if (_core?.CanGoBack == true) _core.GoBack(); };
        _forward.Click += (_, _) => { if (_core?.CanGoForward == true) _core.GoForward(); };
        _reload.Click += (_, _) => _core?.Reload();
        _address.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter)
                return;
            e.Handled = true;
            string? target = BrowserViewModel.ToAddress(_address.Text ?? "");
            if (target is null && _vm is not null)
                _vm.Message = "That is not an address.";
            else
                Go(target);
        };
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_vm is not null)
            _vm.PropertyChanged -= OnViewModelChanged;
        _vm = DataContext as BrowserViewModel;
        if (_vm is null)
            return;
        _vm.PropertyChanged += OnViewModelChanged;
        _send.Command = _vm.SendToWallCommand;
        _message.Text = _vm.Message;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsVisibleProperty && IsVisible)
            EnsureWebView();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (IsEffectivelyVisible)
            EnsureWebView();
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(BrowserViewModel.Message))
            _message.Text = _vm?.Message;
    }

    private void EnsureWebView()
    {
        if (_view is not null || _vm is null || VisualRoot is null)
            return;
        BrowserViewModel vm = _vm;
        var view = new NativeWebView();
        _view = view;
        vm.Web.Attach(view, core =>
        {
            _core = core;
            core.NewWindowRequested += (_, a) => { a.Handled = true; core.Navigate(a.Uri); };
            core.SourceChanged += (_, _) => OnSourceChanged(core.Source);
            core.HistoryChanged += (_, _) =>
            {
                _back.IsEnabled = core.CanGoBack;
                _forward.IsEnabled = core.CanGoForward;
            };
            core.Navigate(vm.StartPage);
            return Task.CompletedTask;
        }, ex =>
        {
            vm.Message = "The browser could not start. Is the Microsoft Edge WebView2 runtime installed?";
            vm.Log.WebViewUnavailable(ex);
        });
        _body.Children.Add(view);
    }

    private void OnSourceChanged(string source)
    {
        if (_vm is null)
            return;
        _vm.CurrentUrl = source;
        _vm.Message = null;
        if (!_address.IsFocused)
            _address.Text = source;
    }

    /// <summary>Screenshot mode: the page as PNG. False before the web view exists.</summary>
    internal async Task<bool> CaptureAsync(Stream png)
    {
        if (_core is null)
            return false;
        await _core.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, png);
        return true;
    }

    private void Go(string? address)
    {
        if (address is not null)
            _core?.Navigate(address);
    }
}

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
