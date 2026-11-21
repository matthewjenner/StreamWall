namespace StreamWall.Core.Tests;

/// <summary>
/// The app icon (Scripts/gen-icons.sh) carries every size Windows uses. The 256x256 frame is
/// required: without it the installer and Explorer show a blurry upscale (RingView's lesson).
/// </summary>
public sealed class IconConventionTests
{
    private static readonly int[] Expected = [256, 128, 64, 48, 40, 32, 24, 20, 16];

    [Fact]
    public void TheIcoHasEverySizeWindowsUses()
    {
        byte[] ico = File.ReadAllBytes(RepositoryPaths.Resolve("Src/StreamWall.App/Assets/icon.ico"));

        // ICONDIR: reserved (0), type (1 = icon), count; then 16-byte entries whose first two
        // bytes are width and height (0 means 256).
        BitConverter.ToUInt16(ico, 0).Should().Be(0);
        BitConverter.ToUInt16(ico, 2).Should().Be(1, "it is an icon, not a cursor");
        int count = BitConverter.ToUInt16(ico, 4);
        var sizes = new List<int>();
        for (int i = 0; i < count; i++)
        {
            int width = ico[6 + i * 16] == 0 ? 256 : ico[6 + i * 16];
            int height = ico[6 + i * 16 + 1] == 0 ? 256 : ico[6 + i * 16 + 1];
            width.Should().Be(height, "every frame is square");
            sizes.Add(width);
        }

        sizes.Should().BeEquivalentTo(Expected);
    }

    [Fact]
    public void TheMasterImageIsKeptForRegenerating() =>
        File.Exists(RepositoryPaths.Resolve("Src/StreamWall.App/Assets/icon.png")).Should().BeTrue();
}
