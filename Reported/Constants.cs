namespace Reported;

public static class Constants
{
    public const string DefaultAppealSuccessGifUrl =
        "https://tenor.com/view/tiger-woods-stare-we-can-do-it-gif-11974968";

    public static readonly Dictionary<string, string> ReportReasons = new()
    {
        { "NA", "Negative Attitude" },
        { "VA", "Verbal Abuse" },
        { "AFK", "AFK" },
        { "IG", "Intentional Griefing" },
        { "HS", "Hate Speech" },
        { "CH", "Cheating" },
        { "ON", "Offensive Name" },
        { "DU", "Dumb" },
        { "CAPS", "Caps" },
        { "ST", "This Streamer Sucks" },
        { "KS", "Kill Steal" },
        { "IL", "Why aren't you in my lobby?" },
        { "WTF", "WTF" },
        {"QCD", "Quincy"}
    };
}