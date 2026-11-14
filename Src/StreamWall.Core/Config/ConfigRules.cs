using StreamWall.Core.Diagnostics;

namespace StreamWall.Core.Config;

/// <summary>Defaults, limits and normalization: what any loaded or edited config is snapped to.</summary>
public static class ConfigRules
{
    /// <summary>New streams start nearly silent, so an unequalized stream never blasts (user request).</summary>
    public const int DefaultVolume = 1;
    public const int DefaultStallTimeoutSeconds = 20;
    public const int MinStallTimeoutSeconds = 5;
    public const int MaxStallTimeoutSeconds = 600;

    /// <summary>A stream or sign-in URL must be absolute http(s).</summary>
    public static bool IsValidUrl(string? url) =>
        Uri.TryCreate(url?.Trim(), UriKind.Absolute, out Uri? uri)
        && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
        && !string.IsNullOrEmpty(uri.Host);

    /// <summary>
    /// Clamps numbers, trims text, turns blank optional text into null, gives duplicate stream ids
    /// new ones, and makes sure there is a usable pseudonym key.
    /// </summary>
    public static AppConfig Normalize(AppConfig config)
    {
        var seen = new HashSet<Guid>();
        var streams = new List<StreamConfig>(config.Streams.Count);
        foreach (StreamConfig s in config.Streams)
        {
            Guid id = s.Id == Guid.Empty || !seen.Add(s.Id) ? Guid.NewGuid() : s.Id;
            seen.Add(id);
            streams.Add(s with
            {
                Id = id,
                Label = (s.Label ?? "").Trim(),
                Url = (s.Url ?? "").Trim(),
                VideoSelector = Blank(s.VideoSelector),
                Volume = Math.Clamp(s.Volume, 0, 100),
            });
        }

        // Walls: at most three, unique ids and names, clamped grids, exactly Rows * Columns cells,
        // and every cell pointing at a stream that still exists (or empty).
        var streamIds = streams.Select(s => s.Id).ToHashSet();
        var wallIds = new HashSet<Guid>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var walls = new List<WallConfig>();
        foreach (WallConfig w in (config.Walls ?? []).Take(WallLayout.MaxWalls))
        {
            int rows = Math.Clamp(w.Rows, 1, WallLayout.MaxRows);
            int columns = Math.Clamp(w.Columns, 1, WallLayout.MaxColumns);
            var cells = new Guid?[rows * columns];
            IReadOnlyList<Guid?> old = w.Cells ?? [];
            for (int i = 0; i < cells.Length && i < old.Count; i++)
                cells[i] = old[i] is Guid id && streamIds.Contains(id) ? id : null;

            string name = (w.Name ?? "").Trim();
            if (name.Length == 0 || !names.Add(name))
            {
                for (int n = 1; ; n++)
                {
                    name = "Wall " + n.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    if (names.Add(name))
                        break;
                }
            }
            walls.Add(w with
            {
                Id = w.Id == Guid.Empty || !wallIds.Add(w.Id) ? Guid.NewGuid() : w.Id,
                Name = name,
                Rows = rows,
                Columns = columns,
                Monitor = Math.Max(0, w.Monitor),
                Bounds = w.Bounds is { IsUsable: true } ? w.Bounds : null,
                Cells = cells,
            });
        }

        SiteConfig site = config.Site ?? new SiteConfig();
        return config with
        {
            Walls = walls,
            Version = AppConfig.CurrentVersion,
            Site = site with
            {
                BrowserUrl = IsValidUrl(site.BrowserUrl) ? site.BrowserUrl!.Trim() : null,
                StallTimeoutSeconds = Math.Clamp(site.StallTimeoutSeconds, MinStallTimeoutSeconds, MaxStallTimeoutSeconds),
            },
            Streams = streams,
            MasterVolume = Math.Clamp(config.MasterVolume, 0, 100),
            NewStreamVolume = Math.Clamp(config.NewStreamVolume, 0, 100),
            PseudonymKey = UsableKey(config.PseudonymKey) ? config.PseudonymKey : Convert.ToBase64String(Pseudonymizer.NewKey()),
        };
    }

    public static Pseudonymizer PseudonymizerFor(AppConfig config) => new(Convert.FromBase64String(config.PseudonymKey));

    private static bool UsableKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return false;
        Span<byte> buffer = stackalloc byte[Pseudonymizer.KeyBytes * 2];
        return Convert.TryFromBase64String(key, buffer, out int written) && written >= 16;
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
