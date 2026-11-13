using System.Reflection;
using StreamWall.App.Web;
using StreamWall.Core.Config;
using StreamWall.Core.Diagnostics;
using StreamWall.Core.Presentation;
using StreamWall.Platform;

namespace StreamWall.App.Services;

/// <summary>
/// The composition root: the real app (DPAPI config, log file, the user's WebView2 profile) or demo
/// mode (memory-only config and log, the demo site, a separate demo profile). Lives on the UI
/// thread; everything it raises arrives there.
/// </summary>
public sealed class AppHost : IDisposable
{
    private static readonly TimeSpan SaveDelay = TimeSpan.FromMilliseconds(400);

    private readonly IDisposable? _ownedSink;
    private CancellationTokenSource? _pendingSave;
    private Task _saving = Task.CompletedTask;

    private AppHost(IConfigStore store, IDiagSink sink, WebEnvironment web, bool demo, IDisposable? ownedSink)
    {
        Demo = demo;
        Sink = sink;
        Web = web;
        _ownedSink = ownedSink;
        // Until the config (and its pseudonym key) loads, names are throwaway but still redacted.
        Log = new DiagLog(sink, new LogRedactor(Pseudonymizer.Ephemeral()));
        Store = store;
        Repository = new ConfigRepository(store, Log);
    }

    public static AppHost CreateReal()
    {
        AppPaths paths = AppPaths.ForCurrentUser();
        var sink = new FileDiagSink(paths.Logs);
        return new AppHost(new DpapiConfigStore(paths.Config), sink, new WebEnvironment(paths.WebView, demo: false), demo: false, ownedSink: sink);
    }

    /// <summary>Memory-only config and log; the demo site; a demo-only WebView2 profile, never the real one.</summary>
    public static AppHost CreateDemo(AppConfig? seed = null)
    {
        string demoProfile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StreamWall", "DemoWebView");
        return new AppHost(new MemoryConfigStore(ConfigDocument.Serialize(seed ?? DemoConfig())), new MemoryDiagSink(),
            new WebEnvironment(demoProfile, demo: true), demo: true, ownedSink: null);
    }

    public bool Demo { get; }

    public DiagLog Log { get; }

    /// <summary>Where log lines go; tests read a demo host's memory sink through it.</summary>
    internal IDiagSink Sink { get; }

    /// <summary>Where the config is saved; tests read a demo host's memory store through it.</summary>
    internal IConfigStore Store { get; }

    public WebEnvironment Web { get; }

    public ConfigRepository Repository { get; }

    public AppConfig Config { get; private set; } = ConfigRules.Normalize(new AppConfig());

    public bool IsLoaded { get; private set; }

    /// <summary>Where the Browser tab opens: its last page, else the demo index, else blank.</summary>
    public string BrowserStart => Config.Site.BrowserUrl ?? (Demo ? WebEnvironment.DemoHome : "about:blank");

    /// <summary>Remembers the Browser tab's page so it reopens there (http(s) pages only).</summary>
    public void RememberBrowserPage(string url)
    {
        if (ConfigRules.IsValidUrl(url) && url != Config.Site.BrowserUrl)
            Update(c => c with { Site = c.Site with { BrowserUrl = url } });
    }

    public static string Version { get; } =
        typeof(AppHost).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";

    public event Action<AppConfig>? ConfigChanged;

    public async Task StartAsync()
    {
        Log.AppStarted(Version, Demo);
        Config = await Repository.LoadAsync();
        IsLoaded = true;
        ConfigChanged?.Invoke(Config);
    }

    /// <summary>
    /// Applies a change at once (the UI updates now) and saves it shortly after, coalescing bursts
    /// such as a volume slider drag. New URLs are registered with the redactor before anything else.
    /// </summary>
    public void Update(Func<AppConfig, AppConfig> change)
    {
        Config = ConfigRules.Normalize(change(Config));
        Repository.Register(Config);
        ConfigChanged?.Invoke(Config);
        ScheduleSave();
    }

    /// <summary>
    /// Send to wall: puts <paramref name="url"/> in <paramref name="cell"/> (adding it to the library
    /// if new) and opens that wall if it was closed.
    /// </summary>
    public void SendToWall(string url, CellRef cell)
    {
        Guid streamId = Guid.Empty;
        Guid? replaced = null;
        Update(c =>
        {
            (AppConfig sent, streamId, replaced) = WallLayout.SendToWall(c, url, cell);
            return WallLayout.UpdateWall(sent, cell.WallId, w => w with { IsOpen = true });
        });
        Log.StreamSent(WallNumber(cell.WallId), cell.Index + 1, streamId, replaced is not null);
    }

    /// <summary>Adds a 2x2 wall (from the picker when there is none yet) and returns the new map.</summary>
    public WallMap AddWall()
    {
        if (WallLayout.CanAddWall(Config))
            Update(c => WallLayout.AddWall(c).Config);
        return WallMap.From(Config);
    }

    public int WallNumber(Guid wallId) => Config.Walls.ToList().FindIndex(w => w.Id == wallId) + 1;

    public async Task ResetPseudonymsAsync()
    {
        await FlushAsync();
        Config = await Repository.ResetPseudonymsAsync(Config);
        ConfigChanged?.Invoke(Config);
    }

    /// <summary>Writes any pending change now (on exit).</summary>
    public async Task FlushAsync()
    {
        if (_pendingSave is { } pending)
        {
            await pending.CancelAsync();
            _pendingSave = null;
            await _saving;
            await SaveNowAsync();
        }
        await _saving;
    }

    private void ScheduleSave()
    {
        _pendingSave?.Cancel();
        var cts = new CancellationTokenSource();
        _pendingSave = cts;
        _saving = SaveLaterAsync(cts.Token);
    }

    private async Task SaveLaterAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(SaveDelay, token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        _pendingSave = null;
        await SaveNowAsync();
    }

    private async Task SaveNowAsync()
    {
        try
        {
            await Repository.SaveAsync(Config);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Logged by the repository; the in-memory config stays and the next change retries.
        }
    }

    /// <summary>Demo mode's starting point: demo streams, one 2x2 wall with three of them on it.</summary>
    public static AppConfig DemoConfig()
    {
        string Page(string path) => $"https://{WebEnvironment.DemoHost}/{path}";
        var a = new StreamConfig { Label = "Demo 1", Url = Page("stream.html?n=1"), Volume = 40 };
        var b = new StreamConfig { Label = "Demo multi", Url = Page("multi.html"), Volume = 60 };
        var c = new StreamConfig { Label = "Demo iframe", Url = Page("iframe.html"), Volume = 30 };
        return new AppConfig
        {
            Streams = [a, b, c],
            Walls = [new WallConfig { Name = "Wall 1", Rows = 2, Columns = 2, Cells = [a.Id, b.Id, c.Id, null] }],
        };
    }

    public void Dispose() => _ownedSink?.Dispose();
}

/// <summary>Demo/test config storage: plain memory, never disk.</summary>
internal sealed class MemoryConfigStore(string? initial) : IConfigStore
{
    public string? Json { get; private set; } = initial;

    public Task<string?> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(Json);

    public Task SaveAsync(string json, CancellationToken cancellationToken = default)
    {
        Json = json;
        return Task.CompletedTask;
    }

    public Task QuarantineAsync(CancellationToken cancellationToken = default)
    {
        Json = null;
        return Task.CompletedTask;
    }
}

/// <summary>Demo/test log: kept in memory so tests can assert on what would have been written.</summary>
internal sealed class MemoryDiagSink : IDiagSink
{
    private readonly Lock _gate = new();
    private readonly List<string> _lines = [];

    public IReadOnlyList<string> Lines
    {
        get { lock (_gate) return [.. _lines]; }
    }

    public void Write(DiagLevel level, string scope, string message)
    {
        lock (_gate)
            _lines.Add($"[{level}] {scope}: {message}");
    }
}
