using System.Globalization;
using System.Text.RegularExpressions;

namespace StreamWall.Core.Tests;

/// <summary>
/// No real site in the repository: every URL in an authored file must have a host on the
/// allowlist (placeholders under <c>example.invalid</c>, localhost, and named build/docs hosts).
/// </summary>
/// <remarks>
/// The allowlist is read from the <c>non-placeholder-url</c> rule in <c>.gitleaks.toml</c>, so the
/// pre-commit hook, CI's history scan and this test can never disagree. To allow a new build or
/// docs host, add it there.
/// </remarks>
public sealed partial class UrlAllowlistConventionTests
{
    [Fact]
    public void EveryUrlHostIsOnTheAllowlist()
    {
        (Regex urlHost, IReadOnlyList<Regex> allowed) = RuleFromGitleaks();
        allowed.Should().NotBeEmpty("the allowlist is read from .gitleaks.toml");

        List<string> offenders = [];
        foreach (string file in RepositoryFiles.Authored())
        {
            string[] lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                foreach (Match m in urlHost.Matches(lines[i]))
                {
                    string host = m.Groups[1].Value;
                    if (!allowed.Any(a => a.IsMatch(host)))
                    {
                        // Report the position only: printing the host would put it in the test log.
                        offenders.Add(string.Format(CultureInfo.InvariantCulture, "{0}:{1}",
                            Path.GetRelativePath(RepositoryPaths.Root, file), i + 1));
                    }
                }
            }
        }

        Assert.True(offenders.Count == 0,
            $"{offenders.Count} URL(s) with a host that is not on the allowlist. Use a placeholder under "
                + "example.invalid, or add a build/docs host to .gitleaks.toml:\n  " + string.Join("\n  ", offenders));
    }

    [Fact]
    public void TheRuleFindsTheRealHostAndRejectsRealLookingOnes()
    {
        (Regex urlHost, IReadOnlyList<Regex> allowed) = RuleFromGitleaks();
        bool Allowed(string host) => allowed.Any(a => a.IsMatch(host));

        urlHost.Match("see https://user:pw@cams.example.invalid/x").Groups[1].Value.Should().Be("cams.example.invalid");
        urlHost.Match("wss://cams.example.invalid:443/").Groups[1].Value.Should().Be("cams.example.invalid");
        Allowed("stream.example.invalid").Should().BeTrue();
        Allowed("github.com").Should().BeTrue();
        Allowed("cams.not-allowed.test").Should().BeFalse();
        Allowed("example.invalid.evil.test").Should().BeFalse();
    }

    /// <summary>The rule's <c>regex</c> and its <c>regexes = [ ... ]</c> allowlist, one '''literal''' per line.</summary>
    private static (Regex UrlHost, IReadOnlyList<Regex> Allowed) RuleFromGitleaks()
    {
        string[] lines = File.ReadAllLines(RepositoryPaths.Resolve(".gitleaks.toml"));
        int rule = Array.FindIndex(lines, l => l.Trim() == "id = \"non-placeholder-url\"");
        int regex = rule < 0 ? -1 : Array.FindIndex(lines, rule + 1, l => l.TrimStart().StartsWith("regex = ", StringComparison.Ordinal));
        int start = rule < 0 ? -1 : Array.FindIndex(lines, rule + 1, l => l.Trim() == "regexes = [");
        if (regex < 0 || start < 0)
            throw new InvalidOperationException(".gitleaks.toml has no non-placeholder-url rule with a regex and a regexes list.");

        var urlHost = new Regex(TomlLiteral().Match(lines[regex]).Groups[1].Value, RegexOptions.CultureInvariant);
        List<Regex> allowed = [];
        for (int i = start + 1; i < lines.Length && lines[i].Trim() != "]"; i++)
        {
            Match m = TomlLiteral().Match(lines[i]);
            if (m.Success)
                allowed.Add(new Regex(m.Groups[1].Value, RegexOptions.CultureInvariant));
        }
        return (urlHost, allowed);
    }

    [GeneratedRegex("'''(.+?)'''")]
    private static partial Regex TomlLiteral();
}
