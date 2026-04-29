namespace Reported.Models;

public sealed record AppealOutcome(
    bool Won,
    int AppealWins,
    int AppealAttempts,
    bool HadNoReports,
    int PenaltyReportsAdded,
    AppealRejectionReason RejectionReason = AppealRejectionReason.None,
    bool IsCriticalWin = false,
    bool IsCriticalFail = false,
    int ReportsAppealed = 1);
