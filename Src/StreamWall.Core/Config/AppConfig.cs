namespace StreamWall.Core.Config;

/// <summary>
/// Everything the user configures. Stored as one JSON document, encrypted for the Windows account
/// (the Platform store); nothing site-specific exists anywhere else. Presets join in M4.
/// </summary>
public sealed record AppConfig
{
    public const int CurrentVersion = 1;

    public int Version { get; init; } = CurrentVersion;

    public SiteConfig Site { get; init; } = new();

    /// <summary>The stream library: everything ever sent to a wall (or pasted), in order.</summary>
    public IReadOnlyList<StreamConfig> Streams { get; init; } = [];

    /// <summary>1-3 walls (windows); see <see cref="WallLayout"/>.</summary>
    public IReadOnlyList<WallConfig> Walls { get; init; } = [];

    /// <summary>0-100, scales every cell's volume.</summary>
    public int MasterVolume { get; init; } = 100;

    public bool MasterMuted { get; init; }

    /// <summary>0-100: the volume a newly sent or pasted stream starts at (Settings).</summary>
    public int NewStreamVolume { get; init; } = ConfigRules.DefaultVolume;

    /// <summary>How streams are labeled from page titles (Settings).</summary>
    public StreamLabelMode LabelMode { get; init; } = StreamLabelMode.PageTitle;

    /// <summary>Base64 key for <see cref="Diagnostics.Pseudonymizer"/>; generated on first run.</summary>
    public string PseudonymKey { get; init; } = "";

    /// <summary>Every URL in the config, for registering with the log redactor.</summary>
    public IEnumerable<string?> Urls()
    {
        yield return Site.BrowserUrl;
        foreach (StreamConfig stream in Streams)
            yield return stream.Url;
    }
}

/// <summary>
/// Site-wide settings. There is no sign-in configuration: the user signs in in the Browser tab like
/// any browser, and a cell that loses its video retries on its own (Docs/DESIGN.md).
/// </summary>
public sealed record SiteConfig
{
    /// <summary>The Browser tab's last page, where it reopens. A URL, so it is redacted like any other.</summary>
    public string? BrowserUrl { get; init; }

    /// <summary>Reload a cell whose video has not advanced for this long.</summary>
    public int StallTimeoutSeconds { get; init; } = ConfigRules.DefaultStallTimeoutSeconds;
}

/// <summary>One stream the wall can show.</summary>
public sealed record StreamConfig
{
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>The stream's name (page title, "Stream N", or the user's). Shown in the UI; never logged.</summary>
    public string Label { get; init; } = "";

    /// <summary>The user typed the label: page titles never replace it (<see cref="StreamLabels"/>).</summary>
    public bool LabelEdited { get; init; }

    public string Url { get; init; } = "";

    /// <summary>Optional CSS selector for the video, when "largest visible" picks wrong.</summary>
    public string? VideoSelector { get; init; }

    /// <summary>0-100.</summary>
    public int Volume { get; init; } = ConfigRules.DefaultVolume;

    public bool Muted { get; init; }
}
