namespace Reported.Persistence;

public sealed class UserReport(ulong discordId, ulong initiatedUserDiscordId, bool confused = false, string? description = null)
{
    public int Id { get; set; }
    public ulong DiscordId { get; set; } = discordId;
    public ulong InitiatedUserDiscordId { get; set; } = initiatedUserDiscordId;
    public bool Confused { get; set; } = confused;
    public string? Description { get; set; } = description;
}