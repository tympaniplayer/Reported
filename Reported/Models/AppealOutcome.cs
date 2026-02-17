namespace Reported.Models;

public sealed record AppealOutcome(
    bool Won,
    int AppealWins,
    int AppealAttempts,
    bool HadNoReports,
    int PenaltyReportsAdded);
