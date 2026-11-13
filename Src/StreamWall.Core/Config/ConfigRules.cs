using StreamWall.Core.Diagnostics;

namespace StreamWall.Core.Config;

/// <summary>Defaults, limits and normalization: what any loaded or edited config is snapped to.</summary>
public static class ConfigRules
{
    public const int DefaultVolume = 50;
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

        SiteConfig site = config.Site ?? new SiteConfig();
        return config with
        {
            Version = AppConfig.CurrentVersion,
            Site = site with
            {
                SignInUrl = Blank(site.SignInUrl),
                SignedOutUrlPattern = Blank(site.SignedOutUrlPattern),
                SignedOutSelector = Blank(site.SignedOutSelector),
                StallTimeoutSeconds = Math.Clamp(site.StallTimeoutSeconds, MinStallTimeoutSeconds, MaxStallTimeoutSeconds),
            },
            Streams = streams,
            MasterVolume = Math.Clamp(config.MasterVolume, 0, 100),
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
