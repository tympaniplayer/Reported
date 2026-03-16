# Data Model: Limit Appeal Abuse

**Branch**: `003-limit-appeal-abuse` | **Date**: 2026-03-13

## Entity Changes

### UserReport (modified)

| Field | Type | New? | Notes |
|-------|------|------|-------|
| Id | int | - | Primary key (unchanged) |
| DiscordId | ulong | - | Reported user (unchanged) |
| DiscordName | string | - | (unchanged) |
| InitiatedUserDiscordId | ulong | - | Reporter (unchanged) |
| InitiatedDiscordName | string | - | (unchanged) |
| Confused | bool | - | (unchanged) |
| Description | string? | - | (unchanged) |
| **HasBeenAppealed** | **bool** | **Yes** | Whether this report has been through the appeal process. Default `true` in migration (existing rows locked). Constructor default `false` (new reports appealable). |

**Derived logic** (not stored):
- **IsSelfReport**: `DiscordId == InitiatedUserDiscordId` — computed at query time
- **IsEligibleForAppeal**: `!HasBeenAppealed && !IsSelfReport` — computed at query time

### AppealRecord (unchanged)

No changes. Continues to track aggregate `AppealWins` and `AppealAttempts` per user.

## New Types

### AppealRejectionReason (new enum)

```
None              — Appeal was processed normally (win or lose)
AllAppealed       — All reports for this user have already been appealed
OnlySelfReports   — Only self-initiated reports remain (not appealable)
```

### AppealOutcome (modified record)

Add `RejectionReason` field (type: `AppealRejectionReason`, default: `None`).

## Migration

**Name**: `AddHasBeenAppealedToUserReport`

- `ALTER TABLE UserReports ADD COLUMN HasBeenAppealed INTEGER NOT NULL DEFAULT 1`
- Default `1` (true) ensures all existing reports are treated as already appealed
- No data migration step needed — SQLite applies the default to existing rows

## Query Changes

**Appeal eligibility query** (replaces current `FirstOrDefaultAsync`):

```
WHERE DiscordId = @userId
  AND HasBeenAppealed = false
  AND DiscordId != InitiatedUserDiscordId
```

**Rejection reason detection** (when no eligible report found):

1. Check if user has ANY reports → if none, existing "no reports" penalty path
2. Check if user has any non-self reports that are unappealed → if none but has unappealed self-reports → `OnlySelfReports`
3. Otherwise → `AllAppealed`
