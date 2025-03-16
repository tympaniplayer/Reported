namespace Reported.Persistence;

public sealed class UserReport(ulong discordId, string? description = null)
{
    public int Id { get; set; }
    public ulong DiscordId { get; set; } = discordId;
    public string? Description { get; set; } = description;
}