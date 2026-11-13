using StreamWall.Core.Diagnostics;

namespace StreamWall.Core.Tests;

/// <summary>Every input here uses placeholder hosts; the point is what never comes out.</summary>
public sealed class LogRedactorTests
{
    private static readonly Pseudonymizer Names = new([.. Enumerable.Range(1, 32).Select(i => (byte)i)]);

    private static LogRedactor Fresh() => new(Names);

    [Theory]
    [InlineData("failed to load https://cams.example.invalid/view/1?token=abc123&user=me")]
    [InlineData("HTTPS://CAMS.EXAMPLE.INVALID/VIEW")]
    [InlineData("socket wss://cams.example.invalid:443/live closed")]
    [InlineData("https://someone:secret@cams.example.invalid/x")]
    [InlineData("media blob:https://cams.example.invalid/1b2c-3d4e")]
    [InlineData("quoted \"https://cams.example.invalid/a\" and (https://cams.example.invalid/b)")]
    [InlineData("<a href='https://cams.example.invalid/c'>")]
    [InlineData("http://cams.example.invalid")]
    public void AnyUrlBecomesItsSitePseudonym(string input)
    {
        string output = Fresh().Redact(input);
        output.Should().NotContainEquivalentOf("cams").And.NotContainEquivalentOf("invalid")
            .And.NotContain("token=").And.NotContain("abc123").And.NotContain("secret");
        output.Should().Contain("[url " + Names.Site("cams.example.invalid") + "]");
    }

    [Fact]
    public void UnconfiguredHostsAreStillHidden()
    {
        // Not registered anywhere: the URL rule alone must catch it.
        Fresh().Redact("redirected to https://other.example.invalid/login").Should().NotContain("other");
    }

    [Fact]
    public void AConfiguredSitesNameIsHiddenEverywhereInAnyCase()
    {
        LogRedactor r = Fresh();
        r.Reset(Names, ["https://live.brandname.example.invalid/channel/1"]);
        string site = Names.Site("live.brandname.example.invalid");

        string output = r.Redact("Brandname says the BRANDNAME session at live.brandname.example.invalid expired");

        output.Should().NotContainEquivalentOf("brandname");
        output.Should().Contain(site);
    }

    [Fact]
    public void GenericHostLabelsStayReadable()
    {
        LogRedactor r = Fresh();
        r.Reset(Names, ["https://live.brandname.example.invalid/x"]);
        r.Redact("live video stream stalled").Should().Be("live video stream stalled");
    }

    [Fact]
    public void BareWwwHostsAreHidden() =>
        Fresh().Redact("see www.brandname.example.invalid for help").Should().NotContain("brandname").And.Contain("[url site-");

    [Theory]
    [InlineData("contact someone@brandname.example.invalid", "someone")]
    [InlineData("password=hunter22 rejected", "hunter22")]
    [InlineData("{\"password\": \"hunter22\"}", "hunter22")]
    [InlineData("Authorization: Bearer abcdefghijklmnop", "abcdefghijklmnop")]
    [InlineData("Cookie: sessionid=abc123def", "abc123def")]
    [InlineData("Set-Cookie: sid=zzz999yyy; Path=/", "zzz999yyy")]
    [InlineData("Cookie: a=1; theme=dark; sessionid=second99", "second99")]
    [InlineData("jwt eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxMjM0In0.c2lnbmF0dXJlLXZhbHVl", "eyJhbGciOiJIUzI1NiJ9")]
    public void CredentialShapesAreMasked(string input, string secret) =>
        Fresh().Redact(input).Should().NotContain(secret);

    [Fact]
    public void RegisteredSecretsAreMasked()
    {
        LogRedactor r = Fresh();
        r.RegisterSecret("correct-horse");
        r.Redact("tried correct-horse").Should().Be("tried " + LogRedactor.Mask);
    }

    [Fact]
    public void ResetForgetsTheOldSite()
    {
        LogRedactor r = Fresh();
        r.Reset(Names, ["https://oldbrand.example.invalid/x"]);
        r.Reset(Names, ["https://newbrand.example.invalid/x"]);
        r.Redact("oldbrand").Should().Be("oldbrand", "terms come from the current config only");
        r.Redact("newbrand").Should().NotContain("newbrand");
    }

    [Fact]
    public void PlainTextPassesThrough() =>
        Fresh().Redact("cell 3 stream-1b2c4d reloaded after 20 s stall").Should().Be("cell 3 stream-1b2c4d reloaded after 20 s stall");
}
