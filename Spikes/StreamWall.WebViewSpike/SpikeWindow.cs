using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Threading;
using Microsoft.Web.WebView2.Core;

namespace StreamWall.WebViewSpike;

internal sealed class SpikeWindow : Window
{
    // Checks mode: one page per cell, each exercising one capability.
    private static readonly (string Page, double Volume)[] CheckPages =
    [
        ("single.html", 0.37),        // 0: player library quirks, volume restore, interactive toggle
        ("multi.html", 0.50),         // 1: several videos; later muted via host, then crashed
        ("iframe.html", 0.25),        // 2: cross-origin iframe player + chat iframe
        ("swap.html", 0.60),          // 3: video element re-created every 8 s
        ("popup.html", 0.40),         // 4: window.open + target=_blank
        ("cookie.html?set=1", 0.30),  // 5: sets the shared cookie
        ("cookie.html", 0.30),        // 6: loads later, must see the cookie
        ("expired.html", 0.30),       // 7: redirects to the sign-in page
    ];
    private const int CookieReadIndex = 6;

    private readonly SpikeOptions _o;
    private readonly IClassicDesktopStyleApplicationLifetime _life;
    private readonly Report _report;
    private readonly Metrics _metrics = new();
    private readonly List<Cell> _cells = [];
    private readonly Grid _grid = new();
    private readonly List<(double At, Action Act)> _schedule = [];
    private readonly List<(string Name, double From, double To)> _phases = [];
    private readonly List<(double T, bool Main, bool Occluder)> _activity = [];
    private readonly DispatcherTimer _timer;
    private readonly string _runId = Guid.NewGuid().ToString("N")[..12];
    private Window? _occluder;
    private double _end;
    private double _nextSample = 2;
    private double _nextActivity = 1;
    private bool _sampling;
    private bool _done;

    public SpikeWindow(SpikeOptions options, IClassicDesktopStyleApplicationLifetime life)
    {
        _o = options;
        _life = life;
        _report = new Report(Path.Combine(options.ReportDir, $"{options.Mode}-{options.Flags}-{DateTime.Now:HHmmss}"));

        Title = "StreamWall spike";
        Width = 1280;
        Height = 720;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Position = new PixelPoint(60, 60);
        Background = Brushes.Black;
        Content = _grid;

        bool checks = options.Mode == "checks";
        var pages = checks ? CheckPages : Enumerable.Repeat((options.Page, 0.30), options.Cells).ToArray();
        int n = pages.Length;
        int cols = n <= 1 ? 1 : n <= 4 ? 2 : n <= 9 ? 3 : 4;
        int rows = (n + cols - 1) / cols;
        for (int r = 0; r < rows; r++) _grid.RowDefinitions.Add(new RowDefinition(GridLength.Star));
        for (int c = 0; c < cols; c++) _grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

        for (int i = 0; i < n; i++)
        {
            var cell = new Cell(i, pages[i].Item1, pages[i].Item2, options, _report, _runId);
            _cells.Add(cell);
            double at = checks && i == CookieReadIndex ? 8.0 : 0.5 + i * 0.3; // staggered start
            int index = i;
            _schedule.Add((at, () => AddCell(_cells[index], cols)));
        }

        if (checks) BuildChecksSchedule(); else BuildLoadSchedule();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _timer.Tick += OnTick;
        Opened += (_, _) => _timer.Start();
    }

    private void AddCell(Cell cell, int cols)
    {
        var border = new Border { Margin = new Thickness(2), Background = Brushes.Black, Child = cell.View };
        Grid.SetRow(border, cell.Index / cols);
        Grid.SetColumn(border, cell.Index % cols);
        _grid.Children.Add(border);
    }

    private void BuildChecksSchedule()
    {
        At(14, () => { foreach (var c in _cells) _ = Capture(c, $"cell{c.Index}.png"); });
        At(16, () =>
        {
            var c = _cells[1];
            if (c.Core is null) return;
            c.Core.IsMuted = true;
            c.Event("host-muted-readback:" + c.Core.IsMuted);
        });
        At(17, () => _ = _cells[0].Core?.ExecuteScriptAsync("window.__swSetInteractive && window.__swSetInteractive(true)"));
        At(18, () => _ = Capture(_cells[0], "cell0-interactive.png"));
        At(19, () => _ = _cells[0].Core?.ExecuteScriptAsync("window.__swSetInteractive && window.__swSetInteractive(false)"));
        At(20, () => _ = Crash(_cells[1]));
        At(24, () => _ = ClearData(_cells[0]));
        At(25, () => { _cells[CookieReadIndex].Core?.Reload(); _cells[CookieReadIndex].Event("reloaded-after-clear"); });
        At(30, () => { _ = Capture(_cells[0], "cell0-after-interactive.png"); _ = Capture(_cells[3], "cell3-after-swaps.png"); });
        _end = 34;
    }

    private void BuildLoadSchedule()
    {
        At(20, () => _ = Capture(_cells[0], "load-cell0.png"));
        if (_o.Occlude)
        {
            _phases.Add(("baseline", 25, 45));
            _phases.Add(("unfocused", 45, 65));
            _phases.Add(("occluded", 65, 85));
            _phases.Add(("recovered", 88, 100));
            At(45, () => ShowOccluder(cover: false));
            At(65, () => ShowOccluder(cover: true));
            At(85, () => { _occluder?.Close(); _occluder = null; Activate(); });
            _end = 100;
        }
        else
        {
            _phases.Add(("baseline", 25, 55));
            _end = 56;
        }
    }

    private void At(double t, Action a) => _schedule.Add((t, a));

    private void ShowOccluder(bool cover)
    {
        _occluder?.Close();
        var w = new Window
        {
            Title = "occluder",
            WindowDecorations = WindowDecorations.None,
            Topmost = true,
            ShowActivated = true,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Background = new SolidColorBrush(Color.FromRgb(40, 40, 40)),
        };
        if (cover)
        {
            // Cover the whole spike window, like a borderless fullscreen game on the same monitor.
            var size = FrameSize ?? Bounds.Size;
            w.Position = Position;
            w.Width = size.Width + 20;
            w.Height = size.Height + 20;
        }
        else
        {
            // Take focus while every cell stays visible: a small window over the title bar.
            w.Position = new PixelPoint(Position.X + 400, Position.Y);
            w.Width = 200;
            w.Height = 20;
        }
        w.Show();
        w.Activate();
        _occluder = w;
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (_done) return;
        double t = _report.Now;

        foreach (var item in _schedule.Where(s => s.At <= t).ToList())
        {
            _schedule.Remove(item);
            try { item.Act(); } catch (Exception ex) { _report.Line($"! scheduled action failed: {ex.GetType().Name}: {ex.Message}"); }
        }

        if (t >= _nextActivity)
        {
            _nextActivity += 1;
            _activity.Add((t, IsActive, _occluder?.IsActive ?? false));
        }

        if (t >= _nextSample && !_sampling)
        {
            _nextSample += 2;
            var env = _cells.FirstOrDefault(c => c.Core is not null)?.Core?.Environment;
            if (env is not null)
            {
                var pids = env.GetProcessInfos().Select(p => p.ProcessId).Append(Environment.ProcessId).Distinct().ToList();
                _sampling = true;
                double at = t;
                Task.Run(() => _metrics.Sample(at, pids)).ContinueWith(_ => Dispatcher.UIThread.Post(() => _sampling = false));
            }
        }

        if (t >= _end)
        {
            _done = true;
            _timer.Stop();
            Finish();
        }
    }

    private async Task Capture(Cell c, string name)
    {
        if (c.Core is null) return;
        try
        {
            await using var fs = File.Create(Path.Combine(_report.Dir, name));
            await c.Core.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, fs);
        }
        catch (Exception ex)
        {
            c.Event("capture-failed:" + ex.GetType().Name);
        }
    }

    private static async Task Crash(Cell c)
    {
        if (c.Core is null) return;
        c.Event("crash-requested");
        try { await c.Core.CallDevToolsProtocolMethodAsync("Page.crash", "{}"); }
        catch (Exception ex) { c.Event("crash-call-ended:" + ex.GetType().Name); }
    }

    private static async Task ClearData(Cell c)
    {
        if (c.Core is null) return;
        try
        {
            await c.Core.Profile.ClearBrowsingDataAsync(
                CoreWebView2BrowsingDataKinds.BrowsingHistory | CoreWebView2BrowsingDataKinds.DiskCache |
                CoreWebView2BrowsingDataKinds.DownloadHistory | CoreWebView2BrowsingDataKinds.GeneralAutofill |
                CoreWebView2BrowsingDataKinds.PasswordAutosave);
            c.Event("cleared-browsing-data-keeping-cookies");
        }
        catch (Exception ex)
        {
            c.Event("clear-failed:" + ex.GetType().Name);
        }
    }

    private void Finish()
    {
        var r = _report;
        var first = _cells.FirstOrDefault(c => c.Core is not null);
        r.Line($"mode={_o.Mode} flags={_o.Flags} cells={_cells.Count} cpus={Environment.ProcessorCount}");
        r.Line($"adapter={_cells[0].View.AdapterInfo}");
        r.Line($"runtime={first?.Core?.Environment.BrowserVersionString}");
        var browserPids = _cells.Where(c => c.Core is not null).Select(c => c.Core!.BrowserProcessId).Distinct().Count();
        r.Line($"cells wrapped={_cells.Count(c => c.Core is not null)}/{_cells.Count}; distinct browser processes={browserPids}");
        if (first?.Core is { } core)
        {
            r.Line($"profile: passwordAutosave={core.Profile.IsPasswordAutosaveEnabled} generalAutofill={core.Profile.IsGeneralAutofillEnabled}" +
                   $" settings.passwordAutosave={core.Settings.IsPasswordAutosaveEnabled}");
        }
        r.Line();

        if (_o.Mode == "checks") SummarizeChecks(r); else SummarizeLoad(r);

        string path = r.Save("summary.txt");
        Console.WriteLine(path);
        foreach (var c in _cells) c.View.IsVisible = false;
        _occluder?.Close();
        _life.Shutdown();
    }

    private void SummarizeChecks(Report r)
    {
        foreach (var c in _cells)
        {
            var tgt = c.Samples.Where(s => s.HasTarget).ToList();
            var last = tgt.LastOrDefault();
            r.Line($"[cell {c.Index}] {c.Page} vol-cfg={c.Volume}");
            if (c.AdapterError is not null) r.Line($"  ADAPTER ERROR: {c.AdapterError}");
            r.Line($"  samples: top={c.Samples.Count(s => s.Top)} frame={c.Samples.Count(s => !s.Top)} withTarget={tgt.Count}");
            if (last is not null)
            {
                r.Line($"  last target sample: vol={last.Vol:0.###} muted={last.Muted} paused={last.Paused} played={last.Played:0.0}s " +
                       $"videoWidth={last.Vw} frames={last.Frames} dropped={last.Dropped} from={(last.Top ? "top" : "frame")}");
            }
            var lastTop = c.Samples.LastOrDefault(s => s.Top);
            if (lastTop is not null)
            {
                r.Line($"  last top sample: vis={lastTop.Vis} raf={lastTop.Raf} videos={lastTop.Videos} iframes={lastTop.Iframes} " +
                       $"cookie={lastTop.Cookie} signedOutSelector={lastTop.SignedOut}");
            }
            if (c.FailedAt is { } failed)
            {
                var after = tgt.FirstOrDefault(s => s.T > failed + 0.5);
                r.Line(after is null ? "  NOT RECOVERED after failure" : $"  recovered: target sample {after.T - failed:0.0}s after failure");
            }
            foreach (var g in c.Events.GroupBy(e => e.Name))
            {
                r.Line($"  event {g.Key} x{g.Count()} (first at {g.First().T:0.0}s)");
            }
        }
    }

    private void SummarizeLoad(Report r)
    {
        foreach (var (name, a, b) in _phases)
        {
            var rates = new List<double>();
            var msgRates = new List<double>();
            double rafSum = 0; int rafN = 0;
            long dropped = 0, frames = 0;
            var vis = new HashSet<string>();
            int paused = 0;
            foreach (var c in _cells)
            {
                var tgt = c.Samples.Where(s => s.HasTarget && s.T >= a && s.T < b).ToList();
                var top = c.Samples.Where(s => s.Top && s.T >= a && s.T < b).ToList();
                if (tgt.Count >= 2) rates.Add((tgt[^1].Played - tgt[0].Played) / (tgt[^1].T - tgt[0].T));
                else rates.Add(0);
                msgRates.Add(top.Count / (b - a));
                foreach (var s in top) { rafSum += s.Raf; rafN++; vis.Add(s.Vis); }
                if (tgt.Count >= 2)
                {
                    dropped += Math.Max(0, (tgt[^1].Dropped ?? 0) - (tgt[0].Dropped ?? 0));
                    frames += Math.Max(0, (tgt[^1].Frames ?? 0) - (tgt[0].Frames ?? 0));
                }
                if (tgt.Any(s => s.Paused == true)) paused++;
            }
            List<ProcSample> ps;
            lock (_metrics.Samples) ps = _metrics.Samples.Where(s => s.T >= a && s.T < b).ToList();
            var act = _activity.Where(x => x.T >= a && x.T < b).ToList();
            r.Line($"[phase {name}] {a}-{b}s");
            r.Line($"  playback rate per cell: min={rates.Min():0.00} avg={rates.Average():0.00} (1.00 = real time); cells with paused samples={paused}");
            r.Line($"  probe msgs/s per cell (timer health): min={msgRates.Min():0.00} avg={msgRates.Average():0.00}; rAF/s avg={(rafN > 0 ? rafSum / rafN : 0):0.0}; visibility={string.Join(",", vis)}");
            r.Line($"  frames rendered={frames} dropped={dropped} ({(frames > 0 ? 100.0 * dropped / frames : 0):0.00}%)");
            if (ps.Count > 0)
            {
                double cores = ps.Average(p => p.CpuCores);
                r.Line($"  cpu={cores:0.00} cores ({100 * cores / Environment.ProcessorCount:0.0}% of machine) private={ps.Average(p => p.PrivateMb):0} MB " +
                       $"gpu3d={ps.Average(p => p.Gpu3d):0.0}% gpuDecode={ps.Average(p => p.GpuDecode):0.0}% processes={ps.Max(p => p.Processes)}");
            }
            if (act.Count > 0)
            {
                r.Line($"  main window active {act.Count(x => x.Main)}/{act.Count}s, occluder active {act.Count(x => x.Occluder)}/{act.Count}s");
            }
        }
    }
}
