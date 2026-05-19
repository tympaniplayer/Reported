namespace Reported.Persistence;

public sealed class UserPreferences(ulong discordId)
{
    public int Id { get; set; }
    public ulong DiscordId { get; set; } = discordId;
    public string? AppealSuccessGifUrl { get; set; }
    public string? AppealFailureGifUrl { get; set; }
}
