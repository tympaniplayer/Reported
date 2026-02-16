# Service Interface Contracts

**Branch**: `002-test-infrastructure` | **Date**: 2026-02-15

These contracts define the public API surface of the extracted service classes. Tests are written against these signatures. `Program.cs` Discord handlers call these methods and translate the `Result<T>` outcomes into Discord responses.

## IRandomProvider

```csharp
public interface IRandomProvider
{
    int Next(int minValue, int maxValue);
}
```

**Contract**: Returns a value in `[minValue, maxValue)` (exclusive upper bound), matching `System.Random.Next` behavior.

## ReportingService

```csharp
public sealed class ReportingService
{
    // Dependencies: ReportedDbContext, IRandomProvider

    public async Task<Result<ReportOutcome>> CreateReport(
        ulong targetDiscordId,
        string targetName,
        ulong initiatorDiscordId,
        string initiatorName,
        string reasonCode);

    public async Task<Result<IReadOnlyList<ReportGroup>>> GetReportsByReporter(
        ulong userDiscordId);

    public async Task<Result<IReadOnlyList<ReportGroup>>> GetReportsByReason(
        ulong userDiscordId);
}
```

### CreateReport Contract

**Inputs**: Target user info, initiator user info, reason alias code.

**Behavior**:
1. Roll for self-report backfire: `random.Next(0, 100) < 5` → 5% chance
   - If triggered: Create 5 reports on the *initiator* (not target) with `confused = true`, using the given reason
2. If no self-report: Roll for critical hit: `random.Next(0, 100) == 1` → 1% chance
   - Critical hit: Create 2 reports on target
   - Normal: Create 1 report on target
3. Query total report count on target and count of this specific reason
4. Return `ReportOutcome` with all details

**Success**: Always (database operations succeed)
**Failure**: Database error (propagated as `Result.Failure`)

### GetReportsByReporter Contract

**Input**: Discord ID of the user to look up.
**Returns**: Reports grouped by `InitiatedDiscordName` with count per group.
**Empty result**: Returns empty list (not failure) if user has no reports.

### GetReportsByReason Contract

**Input**: Discord ID of the user to look up.
**Returns**: Reports grouped by `Description` (reason code) with count per group. Null/empty descriptions reported as "Unknown Reason".
**Empty result**: Returns empty list (not failure) if user has no reports.

## AppealService

```csharp
public sealed class AppealService
{
    // Dependencies: ReportedDbContext, IRandomProvider

    public async Task<Result<AppealOutcome>> ProcessAppeal(
        ulong userDiscordId,
        string userName);

    public async Task<Result<AppealStats>> GetAppealStats(
        ulong userDiscordId);
}
```

### ProcessAppeal Contract

**Inputs**: User's Discord ID and display name.

**Behavior**:
1. Look up first report on user: `Set<UserReport>().FirstOrDefault(r => r.DiscordId == id)`
2. **No reports case**: Create 10 penalty reports on the user with reason "DU" (Dumb), `confused = false`
   - Return `AppealOutcome { HadNoReports = true, PenaltyReportsAdded = 10 }`
   - Do NOT create/update an appeal record
3. **Has reports**: Roll for outcome: `random.Next(0, 100) > 49` → 50% win
   - Find or create `AppealRecord` for this user
   - **Win**: Increment `AppealWins` and `AppealAttempts`, remove the single matched report
   - **Loss**: Increment only `AppealAttempts`, add 1 new report on user (not marked confused)
4. Save changes and return `AppealOutcome`

**Success**: Always (database operations succeed)
**Failure**: Database error (propagated as `Result.Failure`)

### GetAppealStats Contract

**Input**: User's Discord ID.
**Returns**: Wins, attempts, and calculated win rate percentage (rounded integer).
**No record**: Returns `AppealStats { Wins = 0, Attempts = 0, WinRate = 0 }` (not failure).

## Value Types

```csharp
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

public sealed record AppealOutcome(
    bool Won,
    int AppealWins,
    int AppealAttempts,
    bool HadNoReports,
    int PenaltyReportsAdded);

public sealed record ReportGroup(
    string GroupKey,
    string DisplayName,
    int Count);

public sealed record AppealStats(
    int Wins,
    int Attempts,
    int WinRate);
```
