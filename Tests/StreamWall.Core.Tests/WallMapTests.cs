using StreamWall.Core.Config;
using StreamWall.Core.Presentation;

namespace StreamWall.Core.Tests;

public sealed class WallMapTests
{
    [Fact]
    public void TheMapShowsEveryWallWithLabelsAndTheNextEmptyCell()
    {
        AppConfig c = ConfigRules.Normalize(new AppConfig());
        (c, Guid w1) = WallLayout.AddWall(c, 1, 2);
        (c, Guid w2) = WallLayout.AddWall(c, 2, 2);
        (c, Guid s, _) = WallLayout.SendToWall(c, "https://cams.example.invalid/1", new CellRef(w1, 0));
        c = WallLayout.UpdateStream(c, s, x => x with { Label = "Front door" });

        WallMap map = WallMap.From(c);

        map.Walls.Select(w => w.Name).Should().Equal("Wall 1", "Wall 2");
        map.Walls[0].Cells.Select(x => x.Label).Should().Equal("Front door", WallMap.EmptyLabel);
        map.Walls[0].Cells[0].IsEmpty.Should().BeFalse();
        map.Walls[1].Cells.Should().HaveCount(4).And.AllSatisfy(x => x.IsEmpty.Should().BeTrue());
        map.NextEmpty.Should().Be(new CellRef(w1, 1));
        map.Describe(new CellRef(w2, 2)).Should().Be("Wall 2, cell 3");
    }

    [Fact]
    public void TheMapNeverCarriesUrls()
    {
        AppConfig c = ConfigRules.Normalize(new AppConfig());
        (c, Guid w) = WallLayout.AddWall(c, 1, 1);
        c = WallLayout.SendToWall(c, "https://cams.example.invalid/secret-path", new CellRef(w, 0)).Config;

        WallMap.From(c).ToString().Should().NotContain("example.invalid").And.NotContain("secret-path");
    }
}
