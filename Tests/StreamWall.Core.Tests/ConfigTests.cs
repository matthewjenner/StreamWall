using StreamWall.Core.Config;
using StreamWall.Core.Diagnostics;
using StreamWall.Core.Presentation;

namespace StreamWall.Core.Tests;

public sealed class ConfigRulesTests
{
    [Fact]
    public void NormalizeClampsTrimsAndBlanks()
    {
        var config = new AppConfig
        {
            MasterVolume = 140,
            Site = new SiteConfig { BrowserUrl = "not a url", StallTimeoutSeconds = 1 },
            Streams = [new StreamConfig { Label = "  Cam A ", Url = " https://cams.example.invalid/1 ", Volume = -5, VideoSelector = "" }],
        };

        AppConfig n = ConfigRules.Normalize(config);

        n.MasterVolume.Should().Be(100);
        n.Site.BrowserUrl.Should().BeNull("only an http(s) page is kept");
        n.Site.StallTimeoutSeconds.Should().Be(ConfigRules.MinStallTimeoutSeconds);
        n.Streams[0].Label.Should().Be("Cam A");
        n.Streams[0].Url.Should().Be("https://cams.example.invalid/1");
        n.Streams[0].Volume.Should().Be(0);
        n.Streams[0].VideoSelector.Should().BeNull();
    }

    [Fact]
    public void DuplicateAndEmptyStreamIdsGetNewOnes()
    {
        var id = Guid.NewGuid();
        AppConfig n = ConfigRules.Normalize(new AppConfig
        {
            Streams = [new StreamConfig { Id = id }, new StreamConfig { Id = id }, new StreamConfig { Id = Guid.Empty }],
        });
        n.Streams.Select(s => s.Id).Should().OnlyHaveUniqueItems().And.NotContain(Guid.Empty);
        n.Streams[0].Id.Should().Be(id, "the first keeps its id");
    }

    [Fact]
    public void AMissingOrBadPseudonymKeyIsReplacedAndAGoodOneKept()
    {
        string made = ConfigRules.Normalize(new AppConfig()).PseudonymKey;
        Convert.FromBase64String(made).Length.Should().Be(Pseudonymizer.KeyBytes);
        ConfigRules.Normalize(new AppConfig { PseudonymKey = "not base64!" }).PseudonymKey.Should().NotBe("not base64!");
        ConfigRules.Normalize(new AppConfig { PseudonymKey = made }).PseudonymKey.Should().Be(made);
    }

    [Theory]
    [InlineData("https://cams.example.invalid/view/1", true)]
    [InlineData("http://cams.example.invalid", true)]
    [InlineData("  https://cams.example.invalid/x  ", true)]
    [InlineData("cams.example.invalid/view", false)]
    [InlineData("ftp://cams.example.invalid/x", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void OnlyAbsoluteHttpUrlsAreValid(string? url, bool valid) => ConfigRules.IsValidUrl(url).Should().Be(valid);
}

public sealed class ConfigDocumentTests
{
    [Fact]
    public void ARoundTripKeepsEverything()
    {
        var stream = new StreamConfig { Label = "Cam A", Url = "https://cams.example.invalid/1", VideoSelector = "video.main", Volume = 35, Muted = true };
        var config = ConfigRules.Normalize(new AppConfig
        {
            MasterVolume = 80,
            MasterMuted = true,
            Site = new SiteConfig { BrowserUrl = "https://cams.example.invalid/home", StallTimeoutSeconds = 30 },
            Streams = [stream],
        });

        ConfigDocument.TryParse(ConfigDocument.Serialize(config), out AppConfig back, out string? problem).Should().BeTrue();

        problem.Should().BeNull();
        back.Site.Should().Be(config.Site);
        back.Streams.Should().ContainSingle().Which.Should().Be(config.Streams[0]);
        back.MasterVolume.Should().Be(80);
        back.MasterMuted.Should().BeTrue();
        back.PseudonymKey.Should().Be(config.PseudonymKey);
    }

    [Fact]
    public void AnOlderConfigWithRemovedSignInSettingsStillLoads()
    {
        const string old = """{"site":{"signInUrl":"https://cams.example.invalid/login","signedOutUrlPattern":"/login","signedOutSelector":"#f","stallTimeoutSeconds":30},"streams":[]}""";
        ConfigDocument.TryParse(old, out AppConfig config, out _).Should().BeTrue();
        config.Site.StallTimeoutSeconds.Should().Be(30);
        ConfigDocument.Serialize(config).Should().NotContain("signIn").And.NotContain("signedOut");
    }

    [Fact]
    public void TheDocumentIsCamelCase() =>
        ConfigDocument.Serialize(new AppConfig()).Should().Contain("\"masterVolume\"").And.Contain("\"pseudonymKey\"");

    [Fact]
    public void BadJsonGivesAReasonThatQuotesNothing()
    {
        ConfigDocument.TryParse("{ \"streams\": [ brandname", out AppConfig config, out string? problem).Should().BeFalse();
        problem.Should().NotBeNull().And.NotContain("brandname");
        config.Streams.Should().BeEmpty();
    }
}

public sealed class ConfigRepositoryTests
{
    private readonly FakeConfigStore _store = new();
    private readonly ListSink _sink = new();
    private readonly DiagLog _log;
    private readonly ConfigRepository _repo;

    public ConfigRepositoryTests()
    {
        _log = new DiagLog(_sink, new LogRedactor(Pseudonymizer.Ephemeral()));
        _repo = new ConfigRepository(_store, _log);
    }

    [Fact]
    public async Task AFirstRunStartsEmptyAndSavesAKey()
    {
        AppConfig config = await _repo.LoadAsync(TestContext.Current.CancellationToken);
        config.Streams.Should().BeEmpty();
        _store.Saves.Should().Be(1);
        ConfigDocument.TryParse(_store.Json!, out AppConfig stored, out _).Should().BeTrue();
        stored.PseudonymKey.Should().Be(config.PseudonymKey);
        _sink.All.Should().Contain("no config yet");
    }

    [Fact]
    public async Task SavedStreamsLoadBackAndTheirSiteIsRedactedFromThenOn()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        AppConfig saved = await _repo.SaveAsync(new AppConfig { Streams = [new StreamConfig { Label = "A", Url = "https://live.brandname.example.invalid/1" }] }, ct);

        var other = new ConfigRepository(_store, _log);
        AppConfig loaded = await other.LoadAsync(ct);

        loaded.Streams.Should().ContainSingle().Which.Url.Should().Be("https://live.brandname.example.invalid/1");
        loaded.PseudonymKey.Should().Be(saved.PseudonymKey);
        _log.Redactor.Redact("Brandname error").Should().NotContainEquivalentOf("brandname");
    }

    [Fact]
    public async Task ACorruptConfigIsSetAsideWithoutQuotingIt()
    {
        _store.Json = "{ \"streams\": [ \"https://live.brandname.example.invalid/1\" ";
        AppConfig config = await _repo.LoadAsync(TestContext.Current.CancellationToken);

        config.Streams.Should().BeEmpty();
        _store.Quarantined.Should().Be(1);
        _sink.All.Should().Contain("could not be read").And.NotContainEquivalentOf("brandname");
    }

    [Fact]
    public async Task AnUnreadableStoreStartsFresh()
    {
        _store.ThrowOnLoad = "it could not be decrypted for this Windows account";
        AppConfig config = await _repo.LoadAsync(TestContext.Current.CancellationToken);
        config.Streams.Should().BeEmpty();
        _sink.All.Should().Contain("could not be decrypted");
    }

    [Fact]
    public async Task ASaveFailureIsLoggedRedacted()
    {
        _store.FailSave = true;
        Func<Task> save = () => _repo.SaveAsync(new AppConfig(), TestContext.Current.CancellationToken);
        await save.Should().ThrowAsync<IOException>();
        _sink.All.Should().Contain("failed to save config").And.NotContain("cams.example.invalid");
    }

    [Fact]
    public async Task ResettingPseudonymsRenamesEverySite()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        AppConfig before = await _repo.SaveAsync(new AppConfig { Streams = [new StreamConfig { Url = "https://cams.example.invalid/1" }] }, ct);
        string oldName = _log.Redactor.Pseudonyms.Site("cams.example.invalid");

        AppConfig after = await _repo.ResetPseudonymsAsync(before, ct);

        after.PseudonymKey.Should().NotBe(before.PseudonymKey);
        _log.Redactor.Pseudonyms.Site("cams.example.invalid").Should().NotBe(oldName);
        after.Streams.Should().HaveCount(1);
    }
}

public sealed class DiagLogTests
{
    [Fact]
    public void ExceptionTextIsRedactedBeforeItIsWritten()
    {
        var sink = new ListSink();
        var log = new DiagLog(sink, new LogRedactor(Pseudonymizer.Ephemeral()));
        log.UiError("open sign-in", new InvalidOperationException("navigation to https://cams.example.invalid/login?next=/x failed"));
        sink.All.Should().Contain("open sign-in").And.NotContain("cams").And.Contain("[url site-");
    }
}

public sealed class UrlMaskTests
{
    [Theory]
    [InlineData("https://cams.example.invalid/view/1", "https://*****/****")]
    [InlineData("http://a.example.invalid", "http://*****/****")]
    [InlineData("not a url at all", "*****/****")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void TheMaskShowsOnlyTheScheme(string? url, string expected) => UrlMask.Mask(url).Should().Be(expected);

    [Fact]
    public void TheMaskDoesNotRevealLength() =>
        UrlMask.Mask("https://a.example.invalid").Length.Should().Be(UrlMask.Mask("https://a-much-longer-host.example.invalid/with/a/path?q=1").Length);
}
