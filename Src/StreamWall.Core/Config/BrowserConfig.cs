namespace StreamWall.Core.Config;

/// <summary>
/// The Browser tab: its open tabs and their settings. Every address here is site-specific, so it
/// lives only in the encrypted config and is registered with the log redactor (<see cref="AppConfig.Urls"/>).
/// </summary>
public sealed record BrowserConfig
{
    /// <summary>The open tabs' pages, in order (restored on launch when <see cref="RestoreTabs"/>).</summary>
    public IReadOnlyList<string> Tabs { get; init; } = [];

    /// <summary>Index of the selected tab in <see cref="Tabs"/>.</summary>
    public int ActiveTab { get; init; }

    /// <summary>Reopen every tab on launch (default), or start with one tab at the last page.</summary>
    public bool RestoreTabs { get; init; } = true;

    /// <summary>Tabs opened from links come to the front (off: they open in the background, like a middle-click in a browser).</summary>
    public bool FocusNewTabs { get; init; }

    /// <summary>Remember each site's zoom level and restore it in any tab (default on).</summary>
    public bool RememberZoom { get; init; } = true;

    /// <summary>Every tab's width in the strip (fixed, so changing titles and audio never move tabs).</summary>
    public int TabWidth { get; init; } = BrowserRules.DefaultTabWidth;

    /// <summary>Zoom per site: normalized host -> zoom factor (1.0 is never stored).</summary>
    public IReadOnlyDictionary<string, double> SiteZoom { get; init; } = new Dictionary<string, double>();
}

/// <summary>What a wall cell's "Open in Browser" button does with the cell (Settings).</summary>
public enum OpenInBrowserMode
{
    /// <summary>Open the page in a new Browser tab; the cell keeps playing (default).</summary>
    KeepOnWall = 0,

    /// <summary>Open it in a new tab and clear the cell (the stream stays in the library).</summary>
    MoveOffWall = 1,

    /// <summary>Ask each time: open, or move.</summary>
    Ask = 2,
}
