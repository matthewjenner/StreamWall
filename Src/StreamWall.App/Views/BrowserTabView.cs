using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Media;
using Microsoft.Web.WebView2.Core;
using StreamWall.App.ViewModels;
using StreamWall.App.Web;

namespace StreamWall.App.Views;

/// <summary>
/// One Browser tab's web view on the shared profile. Created when the tab is first shown (or at
/// once for a tab opened from a link, which loads in the background); keeps the tab model in step
/// (page, title, audio, history, zoom). Links that would open a window open a tab instead, and the
/// context menu's "Open link in new window" becomes "Open link in new tab". Zoom is remembered per
/// site and restored on navigation (Settings).
/// </summary>
public sealed class BrowserTabView : Panel
{
    private const string NewWindowItem = "openLinkInNewWindow";

    private readonly BrowserTabViewModel _tab;
    private readonly BrowserViewModel _browser;
    private NativeWebView? _view;
    private CoreWebView2? _core;
    private ControllerZoom? _zoom;
    private bool _closed;

    public BrowserTabView(BrowserTabViewModel tab, BrowserViewModel browser)
    {
        _tab = tab;
        _browser = browser;
        Background = Brushes.White;
        tab.PropertyChanged += OnTabChanged;
        tab.ZoomRequested += SetZoom;
    }

    public BrowserTabViewModel Tab => _tab;

    public bool CanGoBack => _core?.CanGoBack == true;

    public bool CanGoForward => _core?.CanGoForward == true;

    /// <summary>Creates the web view (once) and loads the tab's page.</summary>
    public void EnsureCreated()
    {
        if (_view is not null || _closed || VisualRoot is null || !_browser.Web.Enabled)
            return;
        var view = new NativeWebView();
        _view = view;
        _browser.Web.Attach(view, (core, controller) =>
        {
            if (_closed)
                return Task.CompletedTask;
            _core = core;
            _zoom = new ControllerZoom(controller, OnZoomChanged);
            core.IsMuted = _tab.IsMuted;
            core.NewWindowRequested += (_, a) =>
            {
                a.Handled = true;
                _browser.OpenTab(a.Uri, _browser.FocusNewTabs);
            };
            core.ContextMenuRequested += OnContextMenu;
            core.SourceChanged += (_, _) => OnSource(core.Source);
            core.DocumentTitleChanged += (_, _) => _tab.PageTitle = core.DocumentTitle;
            core.IsDocumentPlayingAudioChanged += (_, _) => _tab.IsPlayingAudio = core.IsDocumentPlayingAudio;
            core.HistoryChanged += (_, _) =>
            {
                _tab.CanGoBack = core.CanGoBack;
                _tab.CanGoForward = core.CanGoForward;
            };
            core.Navigate(string.IsNullOrWhiteSpace(_tab.Url) ? _browser.StartPage : _tab.Url);
            return Task.CompletedTask;
        }, ex =>
        {
            _browser.Message = "The browser could not start. Is the Microsoft Edge WebView2 runtime installed?";
            _browser.Log.WebViewUnavailable(ex);
        });
        Children.Add(view);
    }

    public void Navigate(string url) => _core?.Navigate(url);

    public void GoBack()
    {
        if (_core?.CanGoBack == true)
            _core.GoBack();
    }

    public void GoForward()
    {
        if (_core?.CanGoForward == true)
            _core.GoForward();
    }

    public void Reload() => _core?.Reload();

    /// <summary>Screenshot mode: the page as PNG. False before the web view exists.</summary>
    internal async Task<bool> CaptureAsync(Stream png)
    {
        if (_core is null)
            return false;
        await _core.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, png);
        return true;
    }

    /// <summary>The tab was closed: release the web view.</summary>
    public void Close()
    {
        _closed = true;
        _tab.PropertyChanged -= OnTabChanged;
        _tab.ZoomRequested -= SetZoom;
        _zoom?.Dispose();
        _zoom = null;
        _core = null;
        Children.Clear();
        _view = null;
    }

    private void OnSource(string url)
    {
        _tab.Url = url;
        _browser.SaveTabs();
        // A page of a remembered site opens at that site's zoom (100% for others); off: left alone.
        if (_browser.ZoomFor(url) is double zoom && _zoom is not null)
            _zoom.Factor = zoom;
    }

    private void OnZoomChanged(double factor)
    {
        _tab.ZoomPercent = (int)Math.Round(factor * 100);
        _browser.SaveZoom(_core?.Source, factor);
    }

    private void SetZoom(double factor)
    {
        if (_zoom is not null)
            _zoom.Factor = factor;
    }

    /// <summary>Screenshot mode: the controller's zoom right now (null before the web view exists).</summary>
    internal double? ZoomFactor => _zoom?.Factor;

    private void OnTabChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(BrowserTabViewModel.IsMuted) && _core is not null)
            _core.IsMuted = _tab.IsMuted;
    }

    /// <summary>"Open link in new window" becomes "Open link in new tab" (same place in the menu).</summary>
    private void OnContextMenu(object? sender, CoreWebView2ContextMenuRequestedEventArgs e)
    {
        if (_core is null || !e.ContextMenuTarget.HasLinkUri)
            return;
        IList<CoreWebView2ContextMenuItem> items = e.MenuItems;
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i].Name != NewWindowItem)
                continue;
            string link = e.ContextMenuTarget.LinkUri;
            CoreWebView2ContextMenuItem newTab = _core.Environment.CreateContextMenuItem(
                "Open link in new tab", null, CoreWebView2ContextMenuItemKind.Command);
            newTab.CustomItemSelected += (_, _) => _browser.OpenTab(link, _browser.FocusNewTabs);
            items.RemoveAt(i);
            items.Insert(i, newTab);
            return;
        }
    }
}
