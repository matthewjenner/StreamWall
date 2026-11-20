using StreamWall.Core.Config;

namespace StreamWall.Core.Tests;

/// <summary>Every <see cref="StreamLabelMode"/> (Settings), and that a label the user typed is never replaced.</summary>
public sealed class StreamLabelsTests
{
    private const string Url1 = "https://cams.example.invalid/1";
    private const string Url2 = "https://cams.example.invalid/2";

    private static (AppConfig Config, Guid Wall) Setup(StreamLabelMode mode)
    {
        (AppConfig c, Guid wall) = WallLayout.AddWall(ConfigRules.Normalize(new AppConfig { LabelMode = mode }), 1, 2);
        return (c, wall);
    }

    [Theory]
    [InlineData(StreamLabelMode.PageTitle, "Front door - Live")]
    [InlineData(StreamLabelMode.PageTitleNewOnly, "Front door - Live")]
    [InlineData(StreamLabelMode.FollowPageTitle, "Front door - Live")]
    [InlineData(StreamLabelMode.Numbered, "Stream 1")]
    public void ANewStreamIsLabeledByTheMode(StreamLabelMode mode, string expected)
    {
        (AppConfig c, Guid wall) = Setup(mode);
        c = WallLayout.SendToWall(c, Url1, new CellRef(wall, 0), "  Front door   -  Live ").Config;
        c.Streams.Single().Label.Should().Be(expected);
        c.Streams.Single().LabelEdited.Should().BeFalse();
    }

    [Fact]
    public void ABlankTitleFallsBackToNumbered()
    {
        (AppConfig c, Guid wall) = Setup(StreamLabelMode.PageTitle);
        c = WallLayout.SendToWall(c, Url1, new CellRef(wall, 0), "   ").Config;
        c = WallLayout.SendToWall(c, Url2, new CellRef(wall, 1), null).Config;
        c.Streams.Select(s => s.Label).Should().Equal("Stream 1", "Stream 2");
    }

    [Fact]
    public void LongTitlesAreShortened()
    {
        string label = StreamLabels.FromTitle(new string('a', 200))!;
        label.Length.Should().Be(StreamLabels.MaxLength);
        label.Should().EndWith("...");
    }

    [Theory]
    [InlineData(StreamLabelMode.PageTitle, "Stream 1", "Back yard", "Back yard")]         // fills a default label
    [InlineData(StreamLabelMode.PageTitle, "Front door", "Back yard", "Front door")]      // a title label stays put
    [InlineData(StreamLabelMode.PageTitleNewOnly, "Stream 1", "Back yard", "Stream 1")]   // never touches existing
    [InlineData(StreamLabelMode.FollowPageTitle, "Front door", "Back yard", "Back yard")] // follows any change
    [InlineData(StreamLabelMode.FollowPageTitle, "Stream 1", "Back yard", "Back yard")]
    [InlineData(StreamLabelMode.Numbered, "Stream 1", "Back yard", "Stream 1")]           // never uses titles
    public void ACellsPageTitleRelabelsByTheMode(StreamLabelMode mode, string label, string pageTitle, string expected)
    {
        (AppConfig c, Guid wall) = Setup(mode);
        (c, Guid id, _) = WallLayout.SendToWall(c, Url1, new CellRef(wall, 0));
        c = WallLayout.UpdateStream(c, id, s => s with { Label = label });

        StreamLabels.ApplyPageTitle(c, id, pageTitle).Streams.Single().Label.Should().Be(expected);
    }

    [Theory]
    [InlineData(StreamLabelMode.PageTitle)]
    [InlineData(StreamLabelMode.FollowPageTitle)]
    public void ALabelTheUserTypedIsNeverReplaced(StreamLabelMode mode)
    {
        (AppConfig c, Guid wall) = Setup(mode);
        (c, Guid id, _) = WallLayout.SendToWall(c, Url1, new CellRef(wall, 0));
        c = WallLayout.UpdateStream(c, id, s => s with { Label = "Stream 1", LabelEdited = true });
        StreamLabels.ApplyPageTitle(c, id, "Back yard").Should().BeSameAs(c);
    }

    [Fact]
    public void NoChangeReturnsTheSameConfigSoNothingIsSaved()
    {
        (AppConfig c, Guid wall) = Setup(StreamLabelMode.FollowPageTitle);
        (c, Guid id, _) = WallLayout.SendToWall(c, Url1, new CellRef(wall, 0), "Back yard");
        StreamLabels.ApplyPageTitle(c, id, "Back yard").Should().BeSameAs(c);
        StreamLabels.ApplyPageTitle(c, id, "  ").Should().BeSameAs(c);
        StreamLabels.ApplyPageTitle(c, Guid.NewGuid(), "x").Should().BeSameAs(c);
    }

    [Fact]
    public void TheLabelModeDefaultsToPageTitleAndUnknownValuesFallBack()
    {
        ConfigRules.Normalize(new AppConfig()).LabelMode.Should().Be(StreamLabelMode.PageTitle);
        ConfigRules.Normalize(new AppConfig { LabelMode = (StreamLabelMode)99 }).LabelMode.Should().Be(StreamLabelMode.PageTitle);
        ConfigDocument.TryParse("""{"labelMode":42}""", out AppConfig fromFuture, out _).Should().BeTrue();
        fromFuture.LabelMode.Should().Be(StreamLabelMode.PageTitle);
    }

    [Theory]
    [InlineData(StreamLabelMode.PageTitle)]
    [InlineData(StreamLabelMode.PageTitleNewOnly)]
    [InlineData(StreamLabelMode.FollowPageTitle)]
    [InlineData(StreamLabelMode.Numbered)]
    public void TheLabelModeAndEditedFlagRoundTrip(StreamLabelMode mode)
    {
        AppConfig c = ConfigRules.Normalize(new AppConfig
        {
            LabelMode = mode,
            Streams = [new StreamConfig { Url = Url1, Label = "Mine", LabelEdited = true }],
        });
        ConfigDocument.TryParse(ConfigDocument.Serialize(c), out AppConfig back, out _).Should().BeTrue();
        back.LabelMode.Should().Be(mode);
        back.Streams.Single().LabelEdited.Should().BeTrue();
    }
}

public sealed class SwapTests
{
    [Fact]
    public void TwoCellsOnOneWallTradePlaces()
    {
        (AppConfig c, Guid w) = WallLayout.AddWall(ConfigRules.Normalize(new AppConfig()), 2, 2);
        (c, Guid a, _) = WallLayout.SendToWall(c, "https://cams.example.invalid/a", new CellRef(w, 0));
        (c, Guid b, _) = WallLayout.SendToWall(c, "https://cams.example.invalid/b", new CellRef(w, 3));

        c = WallLayout.Swap(c, new CellRef(w, 0), new CellRef(w, 3));

        c.Walls[0].Cells.Should().Equal(b, null, null, a);
    }

    [Fact]
    public void CellsSwapAcrossWalls()
    {
        AppConfig c = ConfigRules.Normalize(new AppConfig());
        (c, Guid w1) = WallLayout.AddWall(c, 1, 1);
        (c, Guid w2) = WallLayout.AddWall(c, 1, 2);
        (c, Guid a, _) = WallLayout.SendToWall(c, "https://cams.example.invalid/a", new CellRef(w1, 0));
        (c, Guid b, _) = WallLayout.SendToWall(c, "https://cams.example.invalid/b", new CellRef(w2, 1));

        c = WallLayout.Swap(c, new CellRef(w1, 0), new CellRef(w2, 1));

        WallLayout.StreamAt(c, new CellRef(w1, 0)).Should().Be(b);
        WallLayout.StreamAt(c, new CellRef(w2, 1)).Should().Be(a);
    }

    [Fact]
    public void SwappingWithAnEmptyCellMovesTheStream()
    {
        (AppConfig c, Guid w) = WallLayout.AddWall(ConfigRules.Normalize(new AppConfig()), 1, 2);
        (c, Guid a, _) = WallLayout.SendToWall(c, "https://cams.example.invalid/a", new CellRef(w, 0));
        c = WallLayout.Swap(c, new CellRef(w, 0), new CellRef(w, 1));
        c.Walls[0].Cells.Should().Equal(null, a);
    }

    [Fact]
    public void SwappingACellWithItselfChangesNothing()
    {
        (AppConfig c, Guid w) = WallLayout.AddWall(ConfigRules.Normalize(new AppConfig()), 1, 1);
        WallLayout.Swap(c, new CellRef(w, 0), new CellRef(w, 0)).Should().BeSameAs(c);
    }

    [Fact]
    public void SwappingWithACellThatDoesNotExistThrows()
    {
        (AppConfig c, Guid w) = WallLayout.AddWall(ConfigRules.Normalize(new AppConfig()), 1, 1);
        FluentActions.Invoking(() => WallLayout.Swap(c, new CellRef(w, 0), new CellRef(w, 5))).Should().Throw<ArgumentException>();
    }
}
