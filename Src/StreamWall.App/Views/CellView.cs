using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Microsoft.Web.WebView2.Core;
using StreamWall.App.ViewModels;
using StreamWall.App.Web;
using StreamWall.Core.Diagnostics;

namespace StreamWall.App.Views;

/// <summary>
/// A cell: a slim strip (the only place controls can go - nothing draws over a native web view)
/// and the web view showing the stream video-only. The web view is created after the cell's start
/// delay, recreated when the cell's stream changes, and gone while the cell is empty.
/// </summary>
public sealed class CellView : DockPanel
{
    private static readonly IBrush StripBackground = new SolidColorBrush(Color.FromRgb(0x14, 0x18, 0x1D));

    private readonly WebEnvironment _web;
    private readonly DiagLog _log;
    private readonly TextBlock _label = new() { VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, Foreground = Brushes.Gainsboro };
    private readonly Avalonia.Controls.Shapes.Ellipse _dot = new() { Width = 8, Height = 8, Margin = new Thickness(6, 0), VerticalAlignment = VerticalAlignment.Center };
    private readonly ToggleButton _mute = Icons.Toggle(Icons.Volume, "Mute", "MuteButton");
    private readonly Slider _volume = new() { Minimum = 0, Maximum = 100, Width = 80, Margin = new Thickness(4, -8, 4, -8), VerticalAlignment = VerticalAlignment.Center, Name = "VolumeSlider" };
    private readonly Button _reload = Icons.Button(Icons.Reload, "Reload", "ReloadButton");
    private readonly Button _swap = Icons.Button(Icons.Swap, "Swap with another cell", "SwapButton");
    private readonly Button _openInBrowser = Icons.Button(Icons.OpenInNew, "Open in a Browser tab", "OpenInBrowserButton");
    private readonly Button _clear = Icons.Button(Icons.Close, "Clear cell (stays in your library)", "ClearButton");
    private readonly Panel _body = new() { Background = Brushes.Black };
    private readonly TextBlock _placeholder = new()
    {
        Text = "Empty - use Send to wall",
        Foreground = Brushes.Gray,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
    };
    private readonly List<CoreWebView2Frame> _frames = [];
    private readonly DispatcherTimer _statusTimer = new() { Interval = TimeSpan.FromSeconds(1) };

    private CellViewModel? _vm;
    private NativeWebView? _view;
    private CoreWebView2? _core;
    private int _generation;
    private bool _syncing;
    private bool _started;
    private DateTime _lastAdvance = DateTime.MinValue;
    private DateTime _lastProbe = DateTime.MinValue;
    private readonly Dictionary<bool, double> _lastPlayed = [];
    private readonly Dictionary<bool, ProbeFacts> _facts = [];
    private DateTime _loggedAt = DateTime.MinValue;
    private readonly DispatcherTimer _audioLog = new() { Interval = TimeSpan.FromMilliseconds(700) };
    private (bool Muted, double Volume)? _loggedAudio;
    private bool _sawTarget;

    public CellView(WebEnvironment web, DiagLog log)
    {
        _web = web;
        _log = log;

        var strip = new DockPanel { Height = 28, Background = StripBackground, Margin = new Thickness(0, 0, 0, 1), ClipToBounds = true };
        var right = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        right.Children.Add(_mute);
        right.Children.Add(_volume);
        right.Children.Add(_reload);
        right.Children.Add(_swap);
        right.Children.Add(_openInBrowser);
        right.Children.Add(_clear);
        DockPanel.SetDock(right, Dock.Right);
        strip.Children.Add(right);
        strip.Children.Add(_dot);
        DockPanel.SetDock(_dot, Dock.Left);
        strip.Children.Add(_label);
        DockPanel.SetDock(strip, Dock.Top);
        Children.Add(strip);
        Children.Add(_body);
        _body.Children.Add(_placeholder);

        _mute.IsCheckedChanged += (_, _) => { if (!_syncing && _vm is not null) _vm.Muted = _mute.IsChecked == true; };
        _volume.ValueChanged += (_, e) => { if (!_syncing && _vm is not null) _vm.Volume = (int)Math.Round(e.NewValue); };
        _clear.Click += (_, _) => _vm?.ClearCommand.Execute(null);
        _reload.Click += (_, _) => _vm?.ReloadCommand.Execute(null);
        _swap.Click += (_, _) => _vm?.SwapCommand.Execute(null);
        _openInBrowser.Click += (_, _) => _vm?.OpenInBrowserCommand.Execute(null);
        _statusTimer.Tick += (_, _) => UpdateStatus();
        _audioLog.Tick += (_, _) => LogAudio();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_vm is not null)
        {
            _vm.PropertyChanged -= OnViewModelChanged;
            _vm.ReloadRequested -= OnReloadRequested;
        }
        _vm = DataContext as CellViewModel;
        if (_vm is null)
            return;
        _vm.PropertyChanged += OnViewModelChanged;
        _vm.ReloadRequested += OnReloadRequested;
        SyncStrip();
    }

    /// <summary>Reload (the strip's button, or Reload all with a stagger delay): a fresh web view.</summary>
    private void OnReloadRequested(TimeSpan delay)
    {
        if (!_started)
            return;
        if (delay > TimeSpan.Zero)
            DispatcherTimer.RunOnce(Recreate, delay);
        else
            Recreate();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _statusTimer.Start();
        if (!_started)
        {
            _started = true;
            TimeSpan delay = _vm?.StartDelay ?? TimeSpan.Zero;
            if (delay > TimeSpan.Zero)
                DispatcherTimer.RunOnce(Recreate, delay);
            else
                Recreate();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _statusTimer.Stop();
        RemoveWebView();
        _started = false;
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(CellViewModel.Url):
            case nameof(CellViewModel.VideoSelector):
                if (_started)
                    Recreate();
                break;
            case nameof(CellViewModel.EffectiveVolume):
                PostVolume();
                ScheduleAudioLog();
                break;
            case nameof(CellViewModel.EffectiveMuted):
                if (_core is not null && _vm is not null)
                    _core.IsMuted = _vm.EffectiveMuted;
                ScheduleAudioLog();
                break;
        }
        SyncStrip();
    }

    /// <summary>Logs the audible state once it has settled (a slider drag is one line, not dozens).</summary>
    private void ScheduleAudioLog()
    {
        _audioLog.Stop();
        _audioLog.Start();
    }

    private void LogAudio()
    {
        _audioLog.Stop();
        if (_vm is null || _vm.IsEmpty)
            return;
        (bool, double) state = (_vm.EffectiveMuted, Math.Round(_vm.EffectiveVolume, 2));
        if (state == _loggedAudio)
            return;
        _loggedAudio = state;
        _log.CellAudio(_vm.WallNumber, _vm.Number, _vm.StreamId, state.Item1, state.Item2);
    }

    private void SyncStrip()
    {
        if (_vm is null)
            return;
        _syncing = true;
        try
        {
            _label.Text = string.Create(CultureInfo.InvariantCulture, $"{_vm.Number}  {_vm.Label}");
            bool filled = !_vm.IsEmpty;
            _mute.IsVisible = _volume.IsVisible = _reload.IsVisible = _swap.IsVisible = _openInBrowser.IsVisible = _clear.IsVisible = filled;
            _mute.IsChecked = _vm.Muted;
            _mute.Content = _vm.Muted ? Icons.Mute : Icons.Volume;
            ToolTip.SetTip(_mute, _vm.Muted ? "Unmute" : "Mute");
            _volume.Value = _vm.Volume;
            _dot.Fill = _vm.Status switch
            {
                "playing" => Brushes.LimeGreen,
                "waiting" => Brushes.Orange,
                "error" => Brushes.Red,
                "no video" => Brushes.SlateGray,
                _ => Brushes.DimGray,
            };
            _dot.IsVisible = filled;
            ToolTip.SetTip(_dot, _vm.Status);
        }
        finally
        {
            _syncing = false;
        }
    }

    private void Recreate()
    {
        RemoveWebView();
        if (_vm?.Url is not { } url || VisualRoot is null || !_web.Enabled)
            return;

        int generation = ++_generation;
        _sawTarget = false;
        _lastAdvance = DateTime.MinValue;
        // The old page's last probe must not count for the new one (it caused a false "no video").
        _lastProbe = DateTime.MinValue;
        _lastPlayed.Clear();
        _facts.Clear();
        _vm.Status = "loading";
        _placeholder.IsVisible = false;

        var view = new NativeWebView { Background = Brushes.Black };
        _view = view;
        string? selector = _vm.VideoSelector;
        _web.Attach(view, async (core, _) =>
        {
            if (generation != _generation)
                return;
            _core = core;
            core.IsMuted = _vm?.EffectiveMuted ?? false;
            core.NewWindowRequested += (_, a) => a.Handled = true;
            core.ProcessFailed += OnProcessFailed;
            core.WebMessageReceived += (_, a) => OnMessage(a.TryGetWebMessageAsString(), top: true);
            // The page title may become the stream's label (Settings > Stream labels); never logged.
            core.DocumentTitleChanged += (_, _) => { if (generation == _generation) _vm?.ReportPageTitle(core.DocumentTitle); };
            core.FrameCreated += (_, a) =>
            {
                CoreWebView2Frame frame = a.Frame;
                _frames.Add(frame);
                frame.WebMessageReceived += (_, fa) => OnMessage(fa.TryGetWebMessageAsString(), top: false);
                frame.Destroyed += (_, _) => _frames.Remove(frame);
            };
            await core.AddScriptToExecuteOnDocumentCreatedAsync(CellScripts.For(selector, _vm?.EffectiveVolume ?? 0.5));
            if (generation == _generation)
                core.Navigate(url);
        }, ex =>
        {
            if (_vm is not null)
                _vm.Status = "error";
            _log.CellFailed(_vm?.WallNumber ?? 0, _vm?.Number ?? 0, ex);
        });
        _body.Children.Add(view);
    }

    /// <summary>Screenshot mode: the web view's picture as PNG. False when the cell shows nothing.</summary>
    internal async Task<bool> CaptureAsync(Stream png)
    {
        if (_core is null)
            return false;
        await _core.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, png);
        return true;
    }

    private void RemoveWebView()
    {
        _generation++;
        _frames.Clear();
        _core = null;
        if (_view is not null)
        {
            _body.Children.Remove(_view);
            _view = null;
        }
        _placeholder.IsVisible = true;
    }

    private void OnProcessFailed(object? sender, CoreWebView2ProcessFailedEventArgs e)
    {
        _log.CellFailed(_vm?.WallNumber ?? 0, _vm?.Number ?? 0, new InvalidOperationException("web process failed: " + e.ProcessFailedKind));
        if (e.ProcessFailedKind is CoreWebView2ProcessFailedKind.RenderProcessExited or CoreWebView2ProcessFailedKind.RenderProcessUnresponsive)
            _core?.Reload();
        else
            Dispatcher.UIThread.Post(Recreate);
    }

    private void PostVolume()
    {
        if (_core is null || _vm is null)
            return;
        string json = JsonSerializer.Serialize(new { k = "volume", v = _vm.EffectiveVolume });
        try
        {
            _core.PostWebMessageAsJson(json);
            foreach (CoreWebView2Frame frame in _frames.ToList())
                frame.PostWebMessageAsJson(json);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            // A frame went away mid-send; the next document picks the volume up from its bootstrap.
        }
    }

    /// <summary>Probe messages: structural facts only (see probe.js). Drives the status dot.</summary>
    private void OnMessage(string? json, bool top)
    {
        if (json is null)
            return;
        try
        {
            using var doc = JsonDocument.Parse(json);
            JsonElement r = doc.RootElement;
            if (!r.TryGetProperty("k", out JsonElement k) || k.GetString() != "probe")
                return;
            _lastProbe = DateTime.UtcNow;
            _facts[top] = new ProbeFacts(
                r.GetProperty("videos").GetInt32(),
                r.GetProperty("iframes").GetInt32(),
                r.GetProperty("hasTarget").GetBoolean(),
                r.TryGetProperty("vw", out JsonElement vw) && vw.ValueKind == JsonValueKind.Number ? vw.GetInt32() : 0,
                r.TryGetProperty("paused", out JsonElement ps) && ps.ValueKind == JsonValueKind.True,
                r.TryGetProperty("muted", out JsonElement mu) && mu.ValueKind == JsonValueKind.True,
                r.TryGetProperty("vol", out JsonElement vo) && vo.ValueKind == JsonValueKind.Number ? vo.GetDouble() : -1);
            if (!r.GetProperty("hasTarget").GetBoolean())
                return;
            _sawTarget = true;
            double played = r.GetProperty("played").GetDouble();
            bool paused = r.TryGetProperty("paused", out JsonElement p) && p.ValueKind == JsonValueKind.True;
            if (_lastPlayed.TryGetValue(top, out double before) && played > before + 0.2 && !paused)
                _lastAdvance = DateTime.UtcNow;
            _lastPlayed[top] = played;
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            // Not ours, or malformed: ignore.
        }
    }

    private void UpdateStatus()
    {
        if (_vm is null || _vm.IsEmpty || _vm.Status == "error")
            return;
        DateTime now = DateTime.UtcNow;
        string status = now - _lastAdvance < TimeSpan.FromSeconds(3) ? "playing"
            : _sawTarget ? "waiting"
            : now - _lastProbe < TimeSpan.FromSeconds(3) && _lastProbe > DateTime.MinValue ? "no video"
            : "loading";
        if (status != _vm.Status || (status == "loading" && now - _loggedAt > TimeSpan.FromSeconds(15)))
        {
            _loggedAt = now;
            _log.CellStatus(_vm.WallNumber, _vm.Number, _vm.StreamId, status, DescribeFacts());
        }
        _vm.Status = status;
    }

    /// <summary>"top: videos=1 iframes=0 target=yes width=1920 paused=no elementMuted=no volume=0.50 hostMuted=no; frame: ..." - numbers and booleans only.</summary>
    private string DescribeFacts()
    {
        static string YesNo(bool b) => b ? "yes" : "no";
        return string.Join("; ", _facts.OrderByDescending(f => f.Key).Select(f => string.Create(CultureInfo.InvariantCulture,
            $"{(f.Key ? "top" : "frame")}: videos={f.Value.Videos} iframes={f.Value.Iframes} target={YesNo(f.Value.HasTarget)} width={f.Value.Width} paused={YesNo(f.Value.Paused)} elementMuted={YesNo(f.Value.ElementMuted)} volume={(f.Value.Volume < 0 ? "-" : f.Value.Volume.ToString("0.00", CultureInfo.InvariantCulture))} hostMuted={YesNo(_core?.IsMuted == true)}")));
    }

    private readonly record struct ProbeFacts(int Videos, int Iframes, bool HasTarget, int Width, bool Paused, bool ElementMuted, double Volume);
}
