using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using StreamWall.Core.Config;

namespace StreamWall.Platform;

/// <summary>
/// The config, encrypted with the Windows Data Protection API for the current user and replaced
/// atomically on every save. DPAPI ties the file to this Windows account: it protects at rest and
/// against other users, not against malware running as the same user, which is the right bar.
/// </summary>
public sealed class DpapiConfigStore(string path) : IConfigStore
{
    /// <summary>Binds the blob to this purpose, so another app's DPAPI blob is never mistaken for it.</summary>
    private static readonly byte[] Entropy = "StreamWall config v1"u8.ToArray();

    public string FilePath => path;

    public async Task<string?> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
            return null;
        byte[] blob = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        try
        {
            return Encoding.UTF8.GetString(ProtectedData.Unprotect(blob, Entropy, DataProtectionScope.CurrentUser));
        }
        catch (CryptographicException)
        {
            // Another Windows user's file, or corrupted. Keep it (the user may want it back on the
            // right account) and start fresh rather than fail.
            await QuarantineAsync(cancellationToken).ConfigureAwait(false);
            throw new ConfigStoreException("it could not be decrypted for this Windows account");
        }
    }

    public Task SaveAsync(string json, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(json);
        byte[] blob = ProtectedData.Protect(Encoding.UTF8.GetBytes(json), Entropy, DataProtectionScope.CurrentUser);
        return AtomicFile.WriteAsync(path, blob, cancellationToken);
    }

    public Task QuarantineAsync(CancellationToken cancellationToken = default)
    {
        if (File.Exists(path))
        {
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            File.Move(path, $"{path}.unreadable-{stamp}", overwrite: true);
        }
        return Task.CompletedTask;
    }
}
