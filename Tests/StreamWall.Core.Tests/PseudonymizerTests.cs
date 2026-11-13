using StreamWall.Core.Diagnostics;

namespace StreamWall.Core.Tests;

public sealed class PseudonymizerTests
{
    private static readonly byte[] Key = [.. Enumerable.Range(1, 32).Select(i => (byte)i)];

    [Fact]
    public void ASiteNameIsShortStableAndHex()
    {
        var p = new Pseudonymizer(Key);
        string name = p.Site("cams.example.invalid");
        name.Should().MatchRegex("^site-[0-9a-f]{6}$");
        new Pseudonymizer(Key).Site("cams.example.invalid").Should().Be(name, "the same key gives the same name across runs");
    }

    [Theory]
    [InlineData("CAMS.Example.Invalid")]
    [InlineData("www.cams.example.invalid")]
    [InlineData("cams.example.invalid.")]
    [InlineData("  cams.example.invalid ")]
    public void SpellingsOfOneHostShareAName(string host) =>
        new Pseudonymizer(Key).Site(host).Should().Be(new Pseudonymizer(Key).Site("cams.example.invalid"));

    [Fact]
    public void DifferentHostsAndKeysGiveDifferentNames()
    {
        var p = new Pseudonymizer(Key);
        p.Site("a.example.invalid").Should().NotBe(p.Site("b.example.invalid"));
        new Pseudonymizer(Pseudonymizer.NewKey()).Site("a.example.invalid").Should().NotBe(p.Site("a.example.invalid"));
    }

    [Fact]
    public void TheNameNeverContainsTheHost()
    {
        string name = new Pseudonymizer(Key).Site("brandname.example.invalid");
        name.Should().NotContain("brand").And.NotContain("example");
    }

    [Fact]
    public void StreamsHaveTheirOwnNames()
    {
        var id = Guid.Parse("3f2a1b4c-0000-4000-8000-000000000001");
        string name = new Pseudonymizer(Key).Stream(id);
        name.Should().MatchRegex("^stream-[0-9a-f]{6}$");
        new Pseudonymizer(Key).Stream(id).Should().Be(name);
    }

    [Fact]
    public void AShortKeyIsRejected() =>
        FluentActions.Invoking(() => new Pseudonymizer(new byte[8])).Should().Throw<ArgumentException>();
}
