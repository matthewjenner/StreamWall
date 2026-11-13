using System.Security.Cryptography;
using System.Text;

namespace StreamWall.Core.Diagnostics;

/// <summary>
/// Stable, irreversible names for sites and streams in logs and diagnostics: <c>site-7f3a9c</c>,
/// <c>stream-1b2c4d</c>. Each is the first 3 bytes of HMAC-SHA256 under a random per-install key
/// kept in the encrypted config, so the same host always gets the same name across runs, but the
/// name cannot be turned back into the host (or recognized) without the key. A new key ("reset
/// pseudonyms") gives every site and stream a new name.
/// </summary>
public sealed class Pseudonymizer
{
    public const int KeyBytes = 32;
    private const int MinKeyBytes = 16;
    private const int NameBytes = 3;

    private readonly byte[] _key;

    public Pseudonymizer(byte[] key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (key.Length < MinKeyBytes)
            throw new ArgumentException($"The key must be at least {MinKeyBytes} bytes.", nameof(key));
        _key = [.. key];
    }

    /// <summary>A fresh random key.</summary>
    public static byte[] NewKey() => RandomNumberGenerator.GetBytes(KeyBytes);

    /// <summary>A pseudonymizer with a throwaway key, for use before the config is loaded.</summary>
    public static Pseudonymizer Ephemeral() => new(NewKey());

    public string Site(string host) => "site-" + Hash("site:" + NormalizeHost(host));

    public string Stream(Guid id) => "stream-" + Hash("stream:" + id.ToString("N"));

    /// <summary>Lowercase, no trailing dot, no leading <c>www.</c>: one site, one name.</summary>
    public static string NormalizeHost(string host)
    {
        string h = host.Trim().TrimEnd('.').ToLowerInvariant();
        return h.StartsWith("www.", StringComparison.Ordinal) ? h[4..] : h;
    }

    private string Hash(string value)
    {
        Span<byte> mac = stackalloc byte[HMACSHA256.HashSizeInBytes];
        HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(value), mac);
        return Convert.ToHexStringLower(mac[..NameBytes]);
    }
}
