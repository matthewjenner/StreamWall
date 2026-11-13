using StreamWall.Core.Config;

namespace StreamWall.Core.Tests;

public sealed class WallLayoutTests
{
    private const string A = "https://cams.example.invalid/view/a";
    private const string B = "https://cams.example.invalid/view/b";

    private static (AppConfig Config, Guid Wall) OneWall(int rows = 2, int columns = 2)
    {
        (AppConfig c, Guid w) = WallLayout.AddWall(ConfigRules.Normalize(new AppConfig()), rows, columns);
        return (c, w);
    }

    [Fact]
    public void WallsAreNamedInOrderUpToThree()
    {
        AppConfig c = ConfigRules.Normalize(new AppConfig());
        for (int i = 0; i < WallLayout.MaxWalls; i++)
            c = WallLayout.AddWall(c).Config;

        c.Walls.Select(w => w.Name).Should().Equal("Wall 1", "Wall 2", "Wall 3");
        WallLayout.CanAddWall(c).Should().BeFalse();
        FluentActions.Invoking(() => WallLayout.AddWall(c)).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ARemovedWallsNameIsReused()
    {
        AppConfig c = ConfigRules.Normalize(new AppConfig());
        (c, Guid first) = WallLayout.AddWall(c);
        c = WallLayout.AddWall(c).Config;
        c = WallLayout.RemoveWall(c, first);
        c = WallLayout.AddWall(c).Config;
        c.Walls.Select(w => w.Name).Should().BeEquivalentTo(["Wall 2", "Wall 1"]);
    }

    [Fact]
    public void ANewWallHasOneEmptyCellPerGridSquare()
    {
        (AppConfig c, Guid wall) = OneWall(3, 4);
        c.Walls.Single(w => w.Id == wall).Cells.Should().HaveCount(12).And.AllSatisfy(x => x.Should().BeNull());
    }

    [Fact]
    public void GridsAreClampedToFourByFour()
    {
        (AppConfig c, Guid wall) = OneWall(9, 0);
        WallConfig w = c.Walls.Single(x => x.Id == wall);
        (w.Rows, w.Columns).Should().Be((4, 1));
    }

    [Fact]
    public void SendToWallAddsTheStreamOnceAndFillsTheCell()
    {
        (AppConfig c, Guid wall) = OneWall();

        (c, Guid first, Guid? replaced) = WallLayout.SendToWall(c, A, new CellRef(wall, 0));
        (c, Guid again, _) = WallLayout.SendToWall(c, A + "/#chat", new CellRef(wall, 3));

        replaced.Should().BeNull();
        again.Should().Be(first, "the same page (ignoring fragment and trailing slash) is one library entry");
        c.Streams.Should().ContainSingle().Which.Label.Should().Be("Stream 1");
        c.Walls[0].Cells.Should().Equal(first, null, null, first);
    }

    [Fact]
    public void SendingToAFilledCellReportsWhatItReplaced()
    {
        (AppConfig c, Guid wall) = OneWall();
        (c, Guid a, _) = WallLayout.SendToWall(c, A, new CellRef(wall, 1));
        (c, Guid b, Guid? replaced) = WallLayout.SendToWall(c, B, new CellRef(wall, 1));

        replaced.Should().Be(a);
        WallLayout.StreamAt(c, new CellRef(wall, 1)).Should().Be(b);
        c.Streams.Should().HaveCount(2, "the replaced stream stays in the library");
        c.Streams.Select(s => s.Label).Should().Equal("Stream 1", "Stream 2");
    }

    [Fact]
    public void ClearingEmptiesTheCellButKeepsTheStream()
    {
        (AppConfig c, Guid wall) = OneWall();
        (c, Guid a, _) = WallLayout.SendToWall(c, A, new CellRef(wall, 2));
        c = WallLayout.Clear(c, new CellRef(wall, 2));
        c.Walls[0].Cells[2].Should().BeNull();
        c.Streams.Should().ContainSingle(s => s.Id == a);
    }

    [Fact]
    public void RemovingAStreamEmptiesEveryCellShowingIt()
    {
        AppConfig c = ConfigRules.Normalize(new AppConfig());
        (c, Guid w1) = WallLayout.AddWall(c);
        (c, Guid w2) = WallLayout.AddWall(c, 1, 1);
        (c, Guid a, _) = WallLayout.SendToWall(c, A, new CellRef(w1, 0));
        c = WallLayout.SendToWall(c, A, new CellRef(w2, 0)).Config;

        c = WallLayout.RemoveStream(c, a);

        c.Streams.Should().BeEmpty();
        c.Walls.SelectMany(w => w.Cells).Should().AllSatisfy(x => x.Should().BeNull());
    }

    [Fact]
    public void NextEmptyLooksAcrossWallsOpenOnesFirst()
    {
        AppConfig c = ConfigRules.Normalize(new AppConfig());
        (c, Guid closed) = WallLayout.AddWall(c, 1, 1);
        (c, Guid open) = WallLayout.AddWall(c, 1, 2);
        c = WallLayout.UpdateWall(c, closed, w => w with { IsOpen = false });

        WallLayout.NextEmpty(c).Should().Be(new CellRef(open, 0));
        c = WallLayout.SendToWall(c, A, new CellRef(open, 0)).Config;
        WallLayout.NextEmpty(c).Should().Be(new CellRef(open, 1));
        c = WallLayout.SendToWall(c, B, new CellRef(open, 1)).Config;
        WallLayout.NextEmpty(c).Should().Be(new CellRef(closed, 0));
        c = WallLayout.SendToWall(c, A, new CellRef(closed, 0)).Config;
        WallLayout.NextEmpty(c).Should().BeNull();
    }

    [Fact]
    public void ResizeKeepsStreamsWhoseRowAndColumnStillExist()
    {
        (AppConfig c, Guid wall) = OneWall(2, 3);
        // Reading order: index = row * 3 + column.
        (c, Guid a, _) = WallLayout.SendToWall(c, A, new CellRef(wall, 1));     // row 0, col 1
        (c, Guid b, _) = WallLayout.SendToWall(c, B, new CellRef(wall, 5));     // row 1, col 2

        AppConfig smaller = WallLayout.Resize(c, wall, 2, 2);
        smaller.Walls[0].Cells.Should().Equal(null, a, null, null);

        AppConfig bigger = WallLayout.Resize(c, wall, 3, 4);
        bigger.Walls[0].Cells.Should().HaveCount(12);
        WallLayout.StreamAt(bigger, new CellRef(wall, 1)).Should().Be(a);
        WallLayout.StreamAt(bigger, new CellRef(wall, 1 * 4 + 2)).Should().Be(b);
        bigger.Streams.Should().HaveCount(2);
    }

    [Fact]
    public void AssigningToACellThatDoesNotExistThrows()
    {
        (AppConfig c, Guid wall) = OneWall();
        FluentActions.Invoking(() => WallLayout.Assign(c, new CellRef(wall, 4), null)).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => WallLayout.Assign(c, new CellRef(Guid.NewGuid(), 0), null)).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void OnlyHttpPagesCanBeSent()
    {
        (AppConfig c, Guid wall) = OneWall();
        FluentActions.Invoking(() => WallLayout.SendToWall(c, "about:blank", new CellRef(wall, 0))).Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("https://cams.example.invalid/view/a", "HTTPS://CAMS.example.invalid/view/a/")]
    [InlineData("https://cams.example.invalid/view/a", "https://cams.example.invalid/view/a#top")]
    public void TheSameStreamIsRecognized(string one, string other) =>
        WallLayout.SameStreamKey(one).Should().Be(WallLayout.SameStreamKey(other));

    [Fact]
    public void DifferentQueriesAreDifferentStreams() =>
        WallLayout.SameStreamKey("https://cams.example.invalid/v?id=1").Should().NotBe(WallLayout.SameStreamKey("https://cams.example.invalid/v?id=2"));
}

public sealed class WallNormalizationTests
{
    [Fact]
    public void CellsArePaddedTrimmedAndCleanedOfMissingStreams()
    {
        var known = new StreamConfig { Url = "https://cams.example.invalid/1" };
        AppConfig c = ConfigRules.Normalize(new AppConfig
        {
            Streams = [known],
            Walls =
            [
                new WallConfig { Rows = 1, Columns = 3, Cells = [known.Id, Guid.NewGuid()] },
                new WallConfig { Rows = 1, Columns = 1, Cells = [known.Id, known.Id, known.Id] },
            ],
        });

        c.Walls[0].Cells.Should().Equal(known.Id, null, null);
        c.Walls[1].Cells.Should().Equal(known.Id);
    }

    [Fact]
    public void AtMostThreeWallsWithUniqueNames()
    {
        AppConfig c = ConfigRules.Normalize(new AppConfig
        {
            Walls = [new() { Name = "Main" }, new() { Name = "main" }, new() { Name = "" }, new() { Name = "Extra" }],
        });
        c.Walls.Should().HaveCount(3);
        c.Walls.Select(w => w.Name).Should().OnlyHaveUniqueItems(n => n.ToUpperInvariant());
        c.Walls[0].Name.Should().Be("Main");
    }

    [Fact]
    public void WallsSurviveARoundTrip()
    {
        AppConfig c = ConfigRules.Normalize(new AppConfig());
        (c, Guid wall) = WallLayout.AddWall(c, 3, 4, monitor: 1);
        c = WallLayout.SendToWall(c, "https://cams.example.invalid/1", new CellRef(wall, 7)).Config;

        ConfigDocument.TryParse(ConfigDocument.Serialize(c), out AppConfig back, out _).Should().BeTrue();

        back.Walls.Should().ContainSingle();
        WallConfig w = back.Walls[0];
        (w.Id, w.Name, w.Rows, w.Columns, w.Monitor, w.IsOpen).Should().Be((wall, "Wall 1", 3, 4, 1, true));
        w.Cells.Should().Equal(c.Walls[0].Cells);
    }
}
