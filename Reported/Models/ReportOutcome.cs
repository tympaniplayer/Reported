namespace Reported.Models;

public sealed record ReportOutcome(
    ulong TargetDiscordId,
    string TargetName,
    string ReasonCode,
    string ReasonDescription,
    int ReportCount,
    int TotalReportsOnTarget,
    int TotalReportsOfThisType,
    bool IsCriticalHit,
    bool IsSelfReport);
