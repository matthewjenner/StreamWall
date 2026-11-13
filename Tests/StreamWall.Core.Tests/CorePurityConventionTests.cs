using System.Text.RegularExpressions;

namespace StreamWall.Core.Tests;

/// <summary>
/// StreamWall.Core stays pure: no packages, no other projects, no native calls. Everything that
/// touches Windows, files, web views or the UI lives in the outer projects behind Core's
/// interfaces, which is what lets the state machines and redaction run in milliseconds with fakes.
/// </summary>
public sealed partial class CorePurityConventionTests
{
    [Fact]
    public void CoreHasNoPackageOrProjectReferences()
    {
        string project = File.ReadAllText(RepositoryPaths.Resolve("Src/StreamWall.Core/StreamWall.Core.csproj"));
        project.Should().NotContain("PackageReference").And.NotContain("ProjectReference");
    }

    [Fact]
    public void CoreMakesNoNativeCalls()
    {
        string core = RepositoryPaths.Resolve("Src/StreamWall.Core") + Path.DirectorySeparatorChar;
        var offenders = RepositoryFiles.Source()
            .Where(f => f.StartsWith(core, StringComparison.OrdinalIgnoreCase) && f.EndsWith(".cs", StringComparison.Ordinal))
            .Where(f => NativeCall().IsMatch(File.ReadAllText(f)))
            .Select(f => Path.GetRelativePath(RepositoryPaths.Root, f))
            .ToList();
        offenders.Should().BeEmpty();
    }

    [GeneratedRegex(@"\[(DllImport|LibraryImport)\b")]
    private static partial Regex NativeCall();
}
