using System.Globalization;

namespace StreamWall.Core.Config;

/// <summary>
/// Everything that changes which stream is where: walls (1-3), their grids, the stream library,
/// and Send to wall. Pure functions over <see cref="AppConfig"/>: each returns a new, normalized
/// config, so the caller saves exactly what it shows.
/// </summary>
public static class WallLayout
{
    public const int MaxWalls = 3;
    public const int MaxRows = 4;
    public const int MaxColumns = 4;

    // ---- Walls ----

    public static bool CanAddWall(AppConfig config) => config.Walls.Count < MaxWalls;

    /// <summary>Adds an open, empty wall named "Wall N" (the lowest unused N).</summary>
    /// <exception cref="InvalidOperationException">There are already <see cref="MaxWalls"/> walls.</exception>
    public static (AppConfig Config, Guid WallId) AddWall(AppConfig config, int rows = 2, int columns = 2, int monitor = 0)
    {
        if (!CanAddWall(config))
            throw new InvalidOperationException($"At most {MaxWalls} walls.");
        var wall = new WallConfig
        {
            Name = NextName(config.Walls.Select(w => w.Name), "Wall"),
            Rows = Math.Clamp(rows, 1, MaxRows),
            Columns = Math.Clamp(columns, 1, MaxColumns),
            Monitor = Math.Max(0, monitor),
        };
        return (ConfigRules.Normalize(config with { Walls = [.. config.Walls, wall] }), wall.Id);
    }

    public static AppConfig RemoveWall(AppConfig config, Guid wallId) =>
        ConfigRules.Normalize(config with { Walls = [.. config.Walls.Where(w => w.Id != wallId)] });

    public static AppConfig UpdateWall(AppConfig config, Guid wallId, Func<WallConfig, WallConfig> change) =>
        ConfigRules.Normalize(config with { Walls = [.. config.Walls.Select(w => w.Id == wallId ? change(w) : w)] });

    /// <summary>
    /// Changes a wall's grid. A stream keeps its row and column if that cell still exists; streams
    /// in rows or columns that go away leave the wall (they stay in the library).
    /// </summary>
    public static AppConfig Resize(AppConfig config, Guid wallId, int rows, int columns)
    {
        rows = Math.Clamp(rows, 1, MaxRows);
        columns = Math.Clamp(columns, 1, MaxColumns);
        return UpdateWall(config, wallId, w =>
        {
            var cells = new Guid?[rows * columns];
            for (int r = 0; r < Math.Min(rows, w.Rows); r++)
            {
                for (int c = 0; c < Math.Min(columns, w.Columns); c++)
                {
                    int from = r * w.Columns + c;
                    if (from < w.Cells.Count)
                        cells[r * columns + c] = w.Cells[from];
                }
            }
            return w with { Rows = rows, Columns = columns, Cells = cells };
        });
    }

    // ---- Cells ----

    public static Guid? StreamAt(AppConfig config, CellRef cell) =>
        config.Walls.FirstOrDefault(w => w.Id == cell.WallId) is { } wall && cell.Index >= 0 && cell.Index < wall.Cells.Count
            ? wall.Cells[cell.Index]
            : null;

    /// <exception cref="ArgumentException">The cell does not exist.</exception>
    public static AppConfig Assign(AppConfig config, CellRef cell, Guid? streamId)
    {
        WallConfig wall = config.Walls.FirstOrDefault(w => w.Id == cell.WallId)
            ?? throw new ArgumentException("No such wall.", nameof(cell));
        if (cell.Index < 0 || cell.Index >= wall.CellCount)
            throw new ArgumentException("No such cell.", nameof(cell));
        return UpdateWall(config, cell.WallId, w =>
        {
            Guid?[] cells = [.. w.Cells];
            cells[cell.Index] = streamId;
            return w with { Cells = cells };
        });
    }

    public static AppConfig Clear(AppConfig config, CellRef cell) => Assign(config, cell, null);

    /// <summary>The first empty cell, open walls first, then walls in order, cells in reading order.</summary>
    public static CellRef? NextEmpty(AppConfig config)
    {
        foreach (WallConfig wall in config.Walls.OrderBy(w => w.IsOpen ? 0 : 1))
        {
            for (int i = 0; i < wall.Cells.Count; i++)
            {
                if (wall.Cells[i] is null)
                    return new CellRef(wall.Id, i);
            }
        }
        return null;
    }

    // ---- Stream library ----

    /// <summary>
    /// The library entry for <paramref name="url"/>: an existing one with the same address, or a new
    /// one labeled by <see cref="StreamLabels.ForNewStream"/> (the page title, or "Stream N").
    /// </summary>
    public static (AppConfig Config, Guid StreamId, bool Added) AddOrReuseStream(AppConfig config, string url, string? pageTitle = null)
    {
        if (!ConfigRules.IsValidUrl(url))
            throw new ArgumentException("Not an http(s) URL.", nameof(url));
        string key = SameStreamKey(url);
        if (config.Streams.FirstOrDefault(s => SameStreamKey(s.Url) == key) is { } existing)
            return (config, existing.Id, false);

        var stream = new StreamConfig
        {
            Url = url.Trim(),
            Label = StreamLabels.ForNewStream(config, pageTitle),
            Volume = config.NewStreamVolume,
        };
        return (ConfigRules.Normalize(config with { Streams = [.. config.Streams, stream] }), stream.Id, true);
    }

    /// <summary>Removes a stream from the library and empties every cell showing it.</summary>
    public static AppConfig RemoveStream(AppConfig config, Guid streamId) =>
        ConfigRules.Normalize(config with { Streams = [.. config.Streams.Where(s => s.Id != streamId)] });

    public static AppConfig UpdateStream(AppConfig config, Guid streamId, Func<StreamConfig, StreamConfig> change) =>
        ConfigRules.Normalize(config with { Streams = [.. config.Streams.Select(s => s.Id == streamId ? change(s) : s)] });

    /// <summary>
    /// Send to wall: the page at <paramref name="url"/> goes into <paramref name="cell"/>, joining
    /// the library if it is new. Returns the stream that was in the cell before, if any.
    /// </summary>
    public static (AppConfig Config, Guid StreamId, Guid? Replaced) SendToWall(AppConfig config, string url, CellRef cell, string? pageTitle = null)
    {
        (AppConfig withStream, Guid streamId, _) = AddOrReuseStream(config, url, pageTitle);
        Guid? replaced = StreamAt(withStream, cell);
        return (Assign(withStream, cell, streamId), streamId, replaced == streamId ? null : replaced);
    }

    /// <summary>
    /// Swaps what two cells show, on the same wall or across walls. Swapping with an empty cell moves
    /// the stream there; swapping a cell with itself changes nothing.
    /// </summary>
    /// <exception cref="ArgumentException">Either cell does not exist.</exception>
    public static AppConfig Swap(AppConfig config, CellRef a, CellRef b)
    {
        if (a == b)
            return config;
        Guid? first = StreamAt(config, a);
        Guid? second = StreamAt(config, b);
        return Assign(Assign(config, a, second), b, first);
    }

    /// <summary>
    /// Two addresses are the same stream when they differ only in case of scheme/host, a trailing
    /// slash, or the fragment.
    /// </summary>
    internal static string SameStreamKey(string url)
    {
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out Uri? uri))
            return url.Trim();
        string path = uri.AbsolutePath.Length > 1 ? uri.AbsolutePath.TrimEnd('/') : "";
        return uri.Scheme.ToLowerInvariant() + "://" + uri.Authority.ToLowerInvariant() + path + uri.Query;
    }

    private static string NextName(IEnumerable<string> taken, string prefix)
    {
        var used = new HashSet<string>(taken, StringComparer.OrdinalIgnoreCase);
        for (int n = 1; ; n++)
        {
            string name = prefix + " " + n.ToString(CultureInfo.InvariantCulture);
            if (!used.Contains(name))
                return name;
        }
    }
}
