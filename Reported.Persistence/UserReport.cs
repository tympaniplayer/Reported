namespace Reported.Persistence;

public sealed class UserReport(ulong discordId,
    string discordName,
    ulong initiatedUserDiscordId,
    string initiatedDiscordName,
    bool confused = false,
    string? description = null)
{
    public int Id { get; set; }
    public ulong DiscordId { get; set; } = discordId;
    public string DiscordName { get; set; } = discordName;
    public ulong InitiatedUserDiscordId { get; set; } = initiatedUserDiscordId;
    public string InitiatedDiscordName { get; set; } = initiatedDiscordName;
    public bool Confused { get; set; } = confused;
    public string? Description { get; set; } = description;
}