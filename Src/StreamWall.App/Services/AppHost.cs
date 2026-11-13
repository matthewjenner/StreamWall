using System.Reflection;
using StreamWall.Core.Config;
using StreamWall.Core.Diagnostics;
using StreamWall.Platform;

namespace StreamWall.App.Services;

/// <summary>
/// The composition root: the real app (DPAPI config, log file) or demo mode (memory only, placeholder
/// streams). Lives on the UI thread; everything it raises arrives there.
/// </summary>
public sealed class AppHost : IDisposable
{
    private readonly IDisposable? _ownedSink;

    private AppHost(IConfigStore store, IDiagSink sink, bool demo, IDisposable? ownedSink)
    {
        Demo = demo;
        Sink = sink;
        _ownedSink = ownedSink;
        // Until the config (and its pseudonym key) loads, names are throwaway but still redacted.
        Log = new DiagLog(sink, new LogRedactor(Pseudonymizer.Ephemeral()));
        Repository = new ConfigRepository(store, Log);
    }

    public static AppHost CreateReal()
    {
        AppPaths paths = AppPaths.ForCurrentUser();
        var sink = new FileDiagSink(paths.Logs);
        return new AppHost(new DpapiConfigStore(paths.Config), sink, demo: false, ownedSink: sink);
    }

    public static AppHost CreateDemo(AppConfig? seed = null) =>
        new(new MemoryConfigStore(ConfigDocument.Serialize(seed ?? DemoConfig())), new MemoryDiagSink(), demo: true, ownedSink: null);

    public bool Demo { get; }

    public DiagLog Log { get; }

    /// <summary>Where log lines go; tests read a demo host's memory sink through it.</summary>
    internal IDiagSink Sink { get; }

    public ConfigRepository Repository { get; }

    public AppConfig Config { get; private set; } = ConfigRules.Normalize(new AppConfig());

    public bool IsLoaded { get; private set; }

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

    public async Task SaveAsync(AppConfig config)
    {
        Config = await Repository.SaveAsync(config);
        ConfigChanged?.Invoke(Config);
    }

    /// <summary>Placeholder streams for demo mode and UI tests (the demo site arrives in M3).</summary>
    public static AppConfig DemoConfig() => new()
    {
        Site = new SiteConfig { SignInUrl = "https://demo.example.invalid/login.html", SignedOutUrlPattern = "/login" },
        Streams =
        [
            new StreamConfig { Label = "Demo A", Url = "https://demo.example.invalid/single.html", Volume = 40 },
            new StreamConfig { Label = "Demo B", Url = "https://demo.example.invalid/multi.html", Volume = 60 },
            new StreamConfig { Label = "Demo C", Url = "https://demo.example.invalid/iframe.html", Volume = 30 },
        ],
    };

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
