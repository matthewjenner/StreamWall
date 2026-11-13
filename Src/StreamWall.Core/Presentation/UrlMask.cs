namespace StreamWall.Core.Presentation;

/// <summary>
/// How a URL is shown until the user reveals it: the scheme and a fixed-length mask, so neither
/// the host nor the length of the URL shows.
/// </summary>
public static class UrlMask
{
    public const string Hidden = "*****/****";

    public static string Mask(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return "";
        return Uri.TryCreate(url.Trim(), UriKind.Absolute, out Uri? uri) && uri.Scheme.Length > 0
            ? uri.Scheme + "://" + Hidden
            : Hidden;
    }
}
