using System.Globalization;
using System.Text.RegularExpressions;

namespace StreamWall.Core.Config;

/// <summary>How streams get their labels (Settings). Stored as a number; unknown values fall back to the default.</summary>
public enum StreamLabelMode
{
    /// <summary>The page's title when sent; a label still "Stream N" takes the title the next time its cell loads (default).</summary>
    PageTitle = 0,

    /// <summary>The page's title when sent; existing labels are left alone.</summary>
    PageTitleNewOnly = 1,

    /// <summary>Labels the user has not edited keep following the page's title as it changes.</summary>
    FollowPageTitle = 2,

    /// <summary>"Stream 1", "Stream 2", ... (page titles are never used).</summary>
    Numbered = 3,
}

/// <summary>
/// Stream labels from page titles. A label the user typed (<see cref="StreamConfig.LabelEdited"/>)
/// is never replaced. Titles are shown in the UI only; like every label they are never logged.
/// </summary>
public static partial class StreamLabels
{
    public const int MaxLength = 60;

    /// <summary>A page title as a label: whitespace collapsed, trimmed, shortened; null if blank.</summary>
    public static string? FromTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return null;
        string t = Whitespace().Replace(title, " ").Trim();
        return t.Length <= MaxLength ? t : t[..(MaxLength - 3)].TrimEnd() + "...";
    }

    /// <summary>Whether a label is still a generated "Stream N".</summary>
    public static bool IsNumbered(string? label) => label is not null && Numbered().IsMatch(label);

    /// <summary>The label for a new stream, by the configured mode.</summary>
    public static string ForNewStream(AppConfig config, string? pageTitle) =>
        config.LabelMode != StreamLabelMode.Numbered && FromTitle(pageTitle) is { } title
            ? title
            : NextNumbered(config.Streams.Select(s => s.Label));

    /// <summary>
    /// A cell showing <paramref name="streamId"/> reported its page title: relabel it if the mode
    /// says so. Returns <paramref name="config"/> itself when nothing changes (so nothing is saved).
    /// </summary>
    public static AppConfig ApplyPageTitle(AppConfig config, Guid streamId, string? pageTitle)
    {
        StreamConfig? stream = config.Streams.FirstOrDefault(s => s.Id == streamId);
        if (stream is null || stream.LabelEdited || FromTitle(pageTitle) is not { } title || stream.Label == title)
            return config;
        bool take = config.LabelMode switch
        {
            StreamLabelMode.PageTitle => IsNumbered(stream.Label),
            StreamLabelMode.FollowPageTitle => true,
            _ => false,
        };
        return take ? WallLayout.UpdateStream(config, streamId, s => s with { Label = title }) : config;
    }

    internal static string NextNumbered(IEnumerable<string> taken)
    {
        var used = new HashSet<string>(taken, StringComparer.OrdinalIgnoreCase);
        for (int n = 1; ; n++)
        {
            string name = "Stream " + n.ToString(CultureInfo.InvariantCulture);
            if (!used.Contains(name))
                return name;
        }
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"^Stream \d+$")]
    private static partial Regex Numbered();
}
