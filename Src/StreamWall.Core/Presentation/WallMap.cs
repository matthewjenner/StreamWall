using System.Globalization;
using StreamWall.Core.Config;

namespace StreamWall.Core.Presentation;

/// <summary>
/// What the Send to wall picker and the Walls tab draw: every wall as a small grid, each cell
/// with its stream's label or "Empty". Labels only - never URLs.
/// </summary>
public sealed record WallMap(IReadOnlyList<WallMapWall> Walls, CellRef? NextEmpty)
{
    public const string EmptyLabel = "Empty";

    public static WallMap From(AppConfig config)
    {
        var labels = config.Streams.ToDictionary(s => s.Id, s => s.Label);
        var walls = config.Walls.Select(w => new WallMapWall(
            w.Id,
            w.Name,
            w.Rows,
            w.Columns,
            w.IsOpen,
            [.. w.Cells.Select((id, i) => new WallMapCell(
                new CellRef(w.Id, i),
                i + 1,
                id,
                id is Guid s && labels.TryGetValue(s, out string? label) ? label : EmptyLabel))])).ToList();
        return new WallMap(walls, WallLayout.NextEmpty(config));
    }

    /// <summary>"Wall 1, cell 3" - how the UI names a cell.</summary>
    public string Describe(CellRef cell) =>
        Walls.FirstOrDefault(w => w.Id == cell.WallId) is { } wall
            ? string.Create(CultureInfo.InvariantCulture, $"{wall.Name}, cell {cell.Index + 1}")
            : "";
}

public sealed record WallMapWall(Guid Id, string Name, int Rows, int Columns, bool IsOpen, IReadOnlyList<WallMapCell> Cells);

public sealed record WallMapCell(CellRef Ref, int Number, Guid? StreamId, string Label)
{
    public bool IsEmpty => StreamId is null;
}
