using System.Text.RegularExpressions;

namespace Reported;

/// <summary>
/// Validates that a user-supplied string is a link to a known GIF host.
/// The bot re-posts these URLs verbatim, so the allowlist keeps us from
/// echoing arbitrary links. Both the shareable page URL and the direct
/// media/CDN URL are accepted for each provider.
/// </summary>
public static partial class GifUrl
{
    [GeneratedRegex(
        """
        ^https://(
            # Tenor
            (www\.)?tenor\.com/view/ |
            media\.tenor\.com/ |
            # Giphy
            (www\.)?giphy\.com/gifs/ |
            (media\d?|i)\.giphy\.com/ |
            # Klipy
            (www\.)?klipy\.com/gifs/ |
            static\d?\.klipy\.com/
        )\S+$
        """,
        RegexOptions.IgnorePatternWhitespace)]
    private static partial Regex Pattern();

    public static bool IsValid(string url) => Pattern().IsMatch(url);
}
