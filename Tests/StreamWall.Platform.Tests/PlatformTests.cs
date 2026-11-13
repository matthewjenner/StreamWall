using System.Security.Cryptography;
using System.Text;
using StreamWall.Core.Config;
using StreamWall.Core.Diagnostics;

namespace StreamWall.Platform.Tests;

/// <summary>A fresh folder under %TEMP% per test, removed afterwards. Never the real app data.</summary>
public abstract class TempFolderTest : IDisposable
{
    protected TempFolderTest()
    {
        Folder = Path.Combine(Path.GetTempPath(), "StreamWallTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Folder);
    }

    protected string Folder { get; }

    public void Dispose()
    {
        try { Directory.Delete(Folder, recursive: true); } catch (IOException) { /* best effort */ }
        GC.SuppressFinalize(this);
    }
}

public sealed class DpapiConfigStoreTests : TempFolderTest
{
    private const string Json = "{\"streams\":[{\"url\":\"https://live.brandname.example.invalid/1\"}]}";

    private string ConfigPath => Path.Combine(Folder, "config.dat");

    [Fact]
    public async Task ItRoundTrips()
    {
        var store = new DpapiConfigStore(ConfigPath);
        await store.SaveAsync(Json, TestContext.Current.CancellationToken);
        (await store.LoadAsync(TestContext.Current.CancellationToken)).Should().Be(Json);
    }

    [Fact]
    public async Task TheFileOnDiskIsNotReadable()
    {
        await new DpapiConfigStore(ConfigPath).SaveAsync(Json, TestContext.Current.CancellationToken);
        byte[] bytes = await File.ReadAllBytesAsync(ConfigPath, TestContext.Current.CancellationToken);
        Encoding.UTF8.GetString(bytes).Should().NotContain("brandname").And.NotContain("streams");
        Encoding.Unicode.GetString(bytes).Should().NotContain("brandname");
    }

    [Fact]
    public async Task NoFileMeansNoConfig() =>
        (await new DpapiConfigStore(ConfigPath).LoadAsync(TestContext.Current.CancellationToken)).Should().BeNull();

    [Fact]
    public async Task AnUnreadableFileIsSetAsideNotDestroyed()
    {
        // Encrypted with different entropy: as unreadable as another Windows user's file.
        byte[] foreign = ProtectedData.Protect("{}"u8.ToArray(), "something else"u8.ToArray(), DataProtectionScope.CurrentUser);
        await File.WriteAllBytesAsync(ConfigPath, foreign, TestContext.Current.CancellationToken);

        Func<Task> load = () => new DpapiConfigStore(ConfigPath).LoadAsync(TestContext.Current.CancellationToken);

        (await load.Should().ThrowAsync<ConfigStoreException>()).Which.Message.Should().Contain("decrypted");
        File.Exists(ConfigPath).Should().BeFalse();
        Directory.GetFiles(Folder, "config.dat.unreadable-*").Should().ContainSingle()
            .Which.Should().Match(f => File.ReadAllBytes(f).SequenceEqual(foreign));
    }

    [Fact]
    public async Task ASaveReplacesTheFileAndLeavesNoTempFiles()
    {
        var store = new DpapiConfigStore(ConfigPath);
        await store.SaveAsync("{\"a\":1}", TestContext.Current.CancellationToken);
        await store.SaveAsync("{\"a\":2}", TestContext.Current.CancellationToken);
        (await store.LoadAsync(TestContext.Current.CancellationToken)).Should().Be("{\"a\":2}");
        Directory.GetFiles(Folder).Should().ContainSingle();
    }
}

public sealed class FileDiagSinkTests : TempFolderTest
{
    [Fact]
    public void LinesAreAppendedWithLevelAndScope()
    {
        using (var sink = new FileDiagSink(Folder))
        {
            sink.Write(DiagLevel.Warn, "Config", "first");
            sink.Write(DiagLevel.Info, "App", "second");
        }
        string[] lines = File.ReadAllLines(Path.Combine(Folder, "streamwall.log"));
        lines.Should().HaveCount(2);
        lines[0].Should().EndWith("[WARN] Config: first");
        lines[1].Should().EndWith("[INFO] App: second");
    }

    [Fact]
    public void TheLogRollsOverAtItsSizeLimit()
    {
        using (var sink = new FileDiagSink(Folder, maxBytes: 200))
        {
            for (int i = 0; i < 20; i++)
                sink.Write(DiagLevel.Info, "App", "line " + i);
        }
        File.Exists(Path.Combine(Folder, "streamwall.1.log")).Should().BeTrue();
        new FileInfo(Path.Combine(Folder, "streamwall.log")).Length.Should().BeLessThan(400);
    }
}

public sealed class AppPathsTests
{
    [Fact]
    public void DataLivesUnderTheUsersProfileNeverBesideTheExe()
    {
        AppPaths paths = AppPaths.ForCurrentUser();
        string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        paths.Config.Should().Be(Path.Combine(roaming, "StreamWall", "App", "config.dat"));
        paths.Logs.Should().Be(Path.Combine(roaming, "StreamWall", "App", "logs"));
        paths.WebView.Should().Be(Path.Combine(local, "StreamWallData", "WebView"));
        paths.Root.Should().NotStartWith(AppContext.BaseDirectory);
    }

    [Fact]
    public void DevBuildsNeverShareTheInstalledAppsData()
    {
        AppPaths installed = AppPaths.ForCurrentUser();
        AppPaths dev = AppPaths.ForCurrentUser(dev: true);
        dev.Config.Should().NotBe(installed.Config);
        dev.Logs.Should().NotBe(installed.Logs);
        dev.WebView.Should().NotBe(installed.WebView);
        dev.Root.Should().EndWith(Path.Combine("StreamWall", "Dev"));
    }

    /// <summary>
    /// Velopack installs into %LOCALAPPDATA%\StreamWall and deletes it on uninstall; data there
    /// would block the install and be wiped with the app (v0.1.0 would not install because of it).
    /// </summary>
    [Fact]
    public void NoDataLivesInVelopacksInstallFolder()
    {
        string install = AppPaths.InstallRoot + Path.DirectorySeparatorChar;
        IEnumerable<string> data =
        [
            AppPaths.ForCurrentUser().Root, AppPaths.ForCurrentUser().WebView,
            AppPaths.ForCurrentUser(dev: true).Root, AppPaths.ForCurrentUser(dev: true).WebView,
            AppPaths.DemoWebView,
        ];
        data.Should().AllSatisfy(p => (p + Path.DirectorySeparatorChar).Should().NotStartWith(install));
        AppPaths.InstallRoot.Should().Be(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StreamWall"));
    }
}
