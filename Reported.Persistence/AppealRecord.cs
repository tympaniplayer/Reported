namespace Reported.Persistence;

public sealed class AppealRecord(ulong discordId, string discordName)
{
    public int Id { get; set; }
    public ulong DiscordId { get; set; } = discordId;
    public string DiscordName { get; set; } = discordName;
    public int AppealWins { get; set; }
    public int AppealAttempts { get; set; }
}
