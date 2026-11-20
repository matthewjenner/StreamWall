using StreamWall.Core.Diagnostics;

namespace StreamWall.Core.Config;

/// <summary>
/// Browser tab rules: which tabs open on launch, saving the tab list, and zoom per site. Pure
/// functions; each returns the same config instance when nothing changes (so nothing is saved).
/// </summary>
public static class BrowserRules
{
    public const int MaxTabs = 30;
    public const double MinZoom = 0.25;
    public const double MaxZoom = 5.0;
    public const int DefaultTabWidth = 200;
    public const int MinTabWidth = 120;
    public const int MaxTabWidth = 360;

    /// <summary>The tabs to open on launch and which is selected (empty: the app opens its start page).</summary>
    public static (IReadOnlyList<string> Tabs, int Active) StartTabs(AppConfig config)
    {
        BrowserConfig b = config.Browser;
        if (b.RestoreTabs && b.Tabs.Count > 0)
            return (b.Tabs, Math.Clamp(b.ActiveTab, 0, b.Tabs.Count - 1));
        return config.Site.BrowserUrl is { } last ? ([last], 0) : ([], 0);
    }

    /// <summary>
    /// Saves the open tabs (only http(s) pages; others are not restorable) and remembers the selected
    /// tab's page as the last page.
    /// </summary>
    public static AppConfig WithTabs(AppConfig config, IReadOnlyList<string?> tabs, int active)
    {
        string? activeUrl = active >= 0 && active < tabs.Count && ConfigRules.IsValidUrl(tabs[active]) ? tabs[active]!.Trim() : null;
        List<string> kept = [.. tabs.Where(ConfigRules.IsValidUrl).Select(t => t!.Trim()).Take(MaxTabs)];
        int keptActive = activeUrl is null ? 0 : Math.Max(0, kept.IndexOf(activeUrl));
        string? last = activeUrl ?? config.Site.BrowserUrl;

        if (kept.SequenceEqual(config.Browser.Tabs) && keptActive == config.Browser.ActiveTab && last == config.Site.BrowserUrl)
            return config;
        return config with
        {
            Browser = config.Browser with { Tabs = kept, ActiveTab = keptActive },
            Site = config.Site with { BrowserUrl = last },
        };
    }

    /// <summary>The zoom to apply to a page: its site's saved zoom, 1.0 if none, or null when zoom is not remembered.</summary>
    public static double? ZoomFor(AppConfig config, string? url)
    {
        if (!config.Browser.RememberZoom || HostOf(url) is not { } host)
            return null;
        return config.Browser.SiteZoom.TryGetValue(host, out double zoom) ? zoom : 1.0;
    }

    /// <summary>The user zoomed a page: remember it for the page's site (1.0 forgets it).</summary>
    public static AppConfig WithZoom(AppConfig config, string? url, double zoom)
    {
        if (!config.Browser.RememberZoom || HostOf(url) is not { } host || double.IsNaN(zoom))
            return config;
        double z = Normalize(zoom);
        bool has = config.Browser.SiteZoom.TryGetValue(host, out double current);
        if ((has && current == z) || (!has && z == 1.0))
            return config;
        var next = new Dictionary<string, double>(config.Browser.SiteZoom, StringComparer.Ordinal);
        if (z == 1.0)
            next.Remove(host);
        else
            next[host] = z;
        return config with { Browser = config.Browser with { SiteZoom = next } };
    }

    /// <summary>Clamped to 25-500% and rounded to whole percent.</summary>
    public static double Normalize(double zoom) => Math.Round(Math.Clamp(zoom, MinZoom, MaxZoom), 2);

    /// <summary>The site a page belongs to: its host, normalized (case, trailing dot, www).</summary>
    public static string? HostOf(string? url) =>
        Uri.TryCreate(url?.Trim(), UriKind.Absolute, out Uri? uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp) && uri.Host.Length > 0
            ? Pseudonymizer.NormalizeHost(uri.Host)
            : null;

    internal static BrowserConfig NormalizeBrowser(BrowserConfig? browser, string? legacyLastPage)
    {
        BrowserConfig b = browser ?? new BrowserConfig();
        List<string> tabs = [.. (b.Tabs ?? []).Where(ConfigRules.IsValidUrl).Select(t => t.Trim()).Take(MaxTabs)];
        // Configs from before tabs (v0.1.x) had one remembered page: it becomes the first tab.
        if (tabs.Count == 0 && ConfigRules.IsValidUrl(legacyLastPage))
            tabs.Add(legacyLastPage!.Trim());

        var zoom = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach ((string key, double value) in b.SiteZoom ?? new Dictionary<string, double>())
        {
            string host = Pseudonymizer.NormalizeHost(key ?? "");
            double z = double.IsNaN(value) ? 1.0 : Normalize(value);
            if (host.Length > 0 && !host.Contains('/', StringComparison.Ordinal) && z != 1.0)
                zoom[host] = z;
        }

        return b with
        {
            Tabs = tabs,
            ActiveTab = tabs.Count == 0 ? 0 : Math.Clamp(b.ActiveTab, 0, tabs.Count - 1),
            TabWidth = Math.Clamp(b.TabWidth, MinTabWidth, MaxTabWidth),
            SiteZoom = zoom,
        };
    }
}
