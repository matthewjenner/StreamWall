using System.Text.RegularExpressions;

namespace StreamWall.Core.Diagnostics;

/// <summary>
/// Scrubs everything headed for a log or the diagnostics view. In order:
/// <list type="number">
/// <item>Any URL (any host, configured or not) becomes <c>[url site-xxxxxx]</c>.</item>
/// <item>Registered terms: every configured host, and the distinctive labels of each host (the
/// site's name, e.g. the "brand" in <c>live.brand.example</c>), become that site's pseudonym, in
/// any case and anywhere in the text. Registered secrets become <see cref="Mask"/>.</item>
/// <item>E-mail addresses and credential shapes (password=, Bearer, JWT, cookie headers) become
/// <see cref="Mask"/>.</item>
/// </list>
/// Over-redaction is acceptable; a leak is not.
/// </summary>
public sealed partial class LogRedactor
{
    public const string Mask = "[redacted]";

    /// <summary>Shorter host labels are not registered: masking them would hit common substrings.</summary>
    public const int MinLabelLength = 4;

    /// <summary>Shorter secrets are not registered, for the same reason.</summary>
    public const int MinSecretLength = 6;

    /// <summary>Host labels that say nothing about which site it is, so they stay readable.</summary>
    private static readonly HashSet<string> GenericLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        "www", "stream", "streams", "live", "cdn", "api", "app", "apps", "login", "auth", "account",
        "accounts", "media", "video", "videos", "player", "static", "assets", "images", "secure",
        "watch", "embed", "chat", "edge", "origin",
    };

    private readonly Lock _gate = new();
    private readonly Dictionary<string, string> _terms = new(StringComparer.OrdinalIgnoreCase);
    private string[] _ordered = [];
    private Pseudonymizer _pseudonyms;

    public LogRedactor(Pseudonymizer pseudonyms) => _pseudonyms = pseudonyms;

    public Pseudonymizer Pseudonyms
    {
        get { lock (_gate) return _pseudonyms; }
    }

    /// <summary>
    /// Replaces the key and every registered term: called whenever the config is loaded or saved,
    /// with every URL in it.
    /// </summary>
    public void Reset(Pseudonymizer pseudonyms, IEnumerable<string?> urls)
    {
        lock (_gate)
        {
            _pseudonyms = pseudonyms;
            _terms.Clear();
            foreach (string? url in urls)
                RegisterUrlLocked(url);
            Reorder();
        }
    }

    public void RegisterUrl(string? url)
    {
        lock (_gate)
        {
            RegisterUrlLocked(url);
            Reorder();
        }
    }

    public void RegisterSecret(string? value)
    {
        if (value is null || value.Length < MinSecretLength)
            return;
        lock (_gate)
        {
            _terms[value] = Mask;
            Reorder();
        }
    }

    public string Redact(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        Pseudonymizer pseudonyms;
        string[] ordered;
        Dictionary<string, string> terms;
        lock (_gate)
        {
            pseudonyms = _pseudonyms;
            ordered = _ordered;
            terms = new Dictionary<string, string>(_terms, StringComparer.OrdinalIgnoreCase);
        }

        string output = Url().Replace(input, m => "[url " + SiteOf(m.Value, pseudonyms) + "]");
        output = BareWww().Replace(output, m => "[url " + pseudonyms.Site(m.Value) + "]");
        foreach (string term in ordered)
        {
            if (output.Contains(term, StringComparison.OrdinalIgnoreCase))
                output = output.Replace(term, terms[term], StringComparison.OrdinalIgnoreCase);
        }
        output = Email().Replace(output, Mask);
        // Token shapes before key/value: "Authorization: Bearer x" must lose x, not just "Bearer".
        output = Jwt().Replace(output, Mask);
        output = Bearer().Replace(output, m => m.Groups[1].Value + Mask);
        output = CookieHeader().Replace(output, m => m.Groups[1].Value + Mask);
        output = KeyValue().Replace(output, m => m.Groups[1].Value + Mask);
        return output;
    }

    private void RegisterUrlLocked(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return;
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out Uri? uri) || string.IsNullOrEmpty(uri.Host))
        {
            // Not a URL (a pattern or a fragment the user typed): mask it whole if long enough.
            if (url.Trim().Length >= MinSecretLength)
                _terms[url.Trim()] = Mask;
            return;
        }

        string site = _pseudonyms.Site(uri.Host);
        string host = Pseudonymizer.NormalizeHost(uri.Host);
        _terms[uri.Host] = site;
        _terms[host] = site;
        string[] labels = host.Split('.');
        // Every label but the top-level domain, if it is distinctive.
        for (int i = 0; i < labels.Length - 1; i++)
        {
            if (labels[i].Length >= MinLabelLength && !GenericLabels.Contains(labels[i]))
                _terms[labels[i]] = site;
        }
    }

    /// <summary>Longest first, so a host is replaced whole before any of its labels.</summary>
    private void Reorder() => _ordered = [.. _terms.Keys.OrderByDescending(k => k.Length)];

    private static string SiteOf(string url, Pseudonymizer pseudonyms) =>
        Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) && !string.IsNullOrEmpty(uri.Host)
            ? pseudonyms.Site(uri.Host)
            : "site-unknown";

    [GeneratedRegex(@"\b(?:https?|wss?|ftp|file)://[^\s""'<>()\[\]{}]+", RegexOptions.IgnoreCase)]
    private static partial Regex Url();

    [GeneratedRegex(@"\bwww\.[a-z0-9-]+(?:\.[a-z0-9-]+)+", RegexOptions.IgnoreCase)]
    private static partial Regex BareWww();

    [GeneratedRegex(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9-]+(?:\.[A-Za-z0-9-]+)+\b")]
    private static partial Regex Email();

    /// <summary><c>key: value</c> / <c>key="value"</c> for credential-like keys and cookie headers.</summary>
    [GeneratedRegex("""((?:password|passwd|pwd|otp|2fa|auth[_-]?code|access[_-]?token|refresh[_-]?token|session[_-]?id|set-cookie|cookie|authorization)["']?\s*[:=]\s*["']?)([^\s"',}]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex KeyValue();

    /// <summary>A cookie header loses its whole value: there can be many cookies on one line.</summary>
    [GeneratedRegex(@"\b((?:set-)?cookie\s*:\s*)[^\r\n]+", RegexOptions.IgnoreCase)]
    private static partial Regex CookieHeader();

    [GeneratedRegex(@"(Bearer\s+)([A-Za-z0-9._~+/=-]{8,})", RegexOptions.IgnoreCase)]
    private static partial Regex Bearer();

    [GeneratedRegex(@"eyJ[A-Za-z0-9._-]{10,}\.[A-Za-z0-9._-]{5,}\.[A-Za-z0-9._-]{5,}")]
    private static partial Regex Jwt();
}
