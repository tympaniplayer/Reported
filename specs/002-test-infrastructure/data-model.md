# Data Model: Comprehensive Test Infrastructure

**Branch**: `002-test-infrastructure` | **Date**: 2026-02-15

## Existing Entities (Unchanged)

### UserReport

Represents a single report filed against a Discord user. No schema changes.

| Field | Type | Constraints | Notes |
| ----- | ---- | ----------- | ----- |
| Id | int | PK, auto-increment | |
| DiscordId | ulong | Indexed (non-unique) | Target user's Discord ID |
| DiscordName | string | Required | Target user's display name |
| InitiatedUserDiscordId | ulong | | Reporter's Discord ID |
| InitiatedDiscordName | string | Required | Reporter's display name |
| Confused | bool | Default: false | True if self-report backfire (5% chance) |
| Description | string? | Nullable | Report reason alias code (e.g., "NA") |

### AppealRecord

Tracks appeal statistics per user. One record per Discord user. No schema changes.

| Field | Type | Constraints | Notes |
| ----- | ---- | ----------- | ----- |
| Id | int | PK, auto-increment | |
| DiscordId | ulong | Unique index | User's Discord ID |
| DiscordName | string | Required | User's display name |
| AppealWins | int | Default: 0 | Successful appeal count |
| AppealAttempts | int | Default: 0 | Total appeal attempts |

## New Abstractions (Service Layer)

### IRandomProvider

Abstraction over `System.Random` to enable deterministic testing of probability-based logic.

```
interface IRandomProvider
    Next(minValue: int, maxValue: int) → int
```

**Production implementation**: Wraps `System.Random` instance.
**Test implementation**: Returns pre-configured sequence of values.

### ReportOutcome (Value Object)

Represents the result of processing a report command. Returned by `ReportingService.CreateReport()`.

| Field | Type | Notes |
| ----- | ---- | ----- |
| TargetDiscordId | ulong | Who was reported (may differ from requested target if self-report triggered) |
| TargetName | string | Display name of target |
| ReasonCode | string | Alias code (e.g., "NA") |
| ReasonDescription | string | Full reason text (e.g., "Negative Attitude") |
| ReportCount | int | Number of reports created (1 normal, 2 critical, 5 self-report) |
| TotalReportsOnTarget | int | Total reports on target after this operation |
| TotalReportsOfThisType | int | Reports of this specific reason after this operation |
| IsCriticalHit | bool | True if critical hit triggered (1% chance) |
| IsSelfReport | bool | True if self-report backfire triggered (5% chance) |

### AppealOutcome (Value Object)

Represents the result of processing an appeal. Returned by `AppealService.ProcessAppeal()`.

| Field | Type | Notes |
| ----- | ---- | ----- |
| Won | bool | True if appeal was successful |
| AppealWins | int | Total wins after this appeal |
| AppealAttempts | int | Total attempts after this appeal |
| HadNoReports | bool | True if user had no reports (triggers penalty) |
| PenaltyReportsAdded | int | Reports added as penalty (10 if no reports, 1 if lost appeal, 0 if won) |

### ReportGroup (Value Object)

Represents a grouped aggregation of reports. Used by statistics queries.

| Field | Type | Notes |
| ----- | ---- | ----- |
| GroupKey | string | The grouping value (reporter name or reason code) |
| DisplayName | string | Human-readable label |
| Count | int | Number of reports in this group |

### AppealStats (Value Object)

Represents appeal statistics for a user. Returned by `AppealService.GetAppealStats()`.

| Field | Type | Notes |
| ----- | ---- | ----- |
| Wins | int | Total successful appeals |
| Attempts | int | Total appeal attempts |
| WinRate | int | Percentage (0-100), rounded |

## Service Contracts

### ReportingService

| Method | Parameters | Returns | Description |
| ------ | ---------- | ------- | ----------- |
| CreateReport | targetDiscordId: ulong, targetName: string, initiatorDiscordId: ulong, initiatorName: string, reasonCode: string | Result\<ReportOutcome\> | Process a report with self-report (5%) and critical hit (1%) mechanics |
| GetReportsByReporter | userDiscordId: ulong | Result\<IReadOnlyList\<ReportGroup\>\> | Group reports on user by reporter name |
| GetReportsByReason | userDiscordId: ulong | Result\<IReadOnlyList\<ReportGroup\>\> | Group reports on user by reason |

### AppealService

| Method | Parameters | Returns | Description |
| ------ | ---------- | ------- | ----------- |
| ProcessAppeal | userDiscordId: ulong, userName: string | Result\<AppealOutcome\> | Process appeal with 50/50 win/loss; handles no-reports penalty |
| GetAppealStats | userDiscordId: ulong | Result\<AppealStats\> | Get win/loss/rate statistics for a user |
