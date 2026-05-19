using System.Text.RegularExpressions;

namespace Reported;

public static partial class TenorUrl
{
    [GeneratedRegex(@"^https://(www\.)?(tenor\.com/view/|media\.tenor\.com/)\S+$")]
    private static partial Regex Pattern();

    public static bool IsValid(string url) => Pattern().IsMatch(url);
}
