namespace StreamWall.Core.Config;

/// <summary>
/// One wall: a window holding a rows x columns grid of cells. <see cref="Cells"/> is in reading
/// order (row by row) and always has exactly <c>Rows * Columns</c> entries after normalization;
/// each is a stream id from the library, or null for an empty cell.
/// </summary>
public sealed record WallConfig
{
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>"Wall 1" by default; the user can rename it. Shown in the UI and picker, never logged.</summary>
    public string Name { get; init; } = "";

    public int Rows { get; init; } = 2;

    public int Columns { get; init; } = 2;

    /// <summary>Index into the system's monitors; out of range falls back to the primary at runtime.</summary>
    public int Monitor { get; init; }

    /// <summary>Fill the monitor (borderless) rather than a normal, movable window.</summary>
    public bool Fullscreen { get; init; } = true;

    /// <summary>
    /// Where the user last put the wall as a window (not fullscreen); null centers it on its monitor.
    /// Cleared when the monitor changes.
    /// </summary>
    public WallBounds? Bounds { get; init; }

    /// <summary>Whether the wall's window is open; open walls reopen on launch.</summary>
    public bool IsOpen { get; init; } = true;

    public IReadOnlyList<Guid?> Cells { get; init; } = [];

    public int CellCount => Rows * Columns;
}

/// <summary>A windowed wall's position (screen pixels) and size (device-independent units).</summary>
public sealed record WallBounds(int X, int Y, double Width, double Height)
{
    public const double MinWidth = 200;
    public const double MinHeight = 150;

    public bool IsUsable => Width >= MinWidth && Height >= MinHeight;
}

/// <summary>A cell: which wall, and its index in reading order.</summary>
public readonly record struct CellRef(Guid WallId, int Index);
