# Data Model: Appeal Count

**Feature**: 001-appeal-count
**Date**: 2026-02-10

## Entities

### AppealRecord

Represents a single user's cumulative appeal history. One record
per Discord user. Created on first real appeal attempt.

| Field | Type | Constraints | Description |
|-------|------|-------------|-------------|
| Id | int | PK, auto-increment | Internal row identifier |
| DiscordId | ulong | Indexed, unique | Discord user snowflake ID |
| DiscordName | string | Required | Discord display name (for logging/debugging) |
| AppealWins | int | Default: 0, >= 0 | Number of successful appeals |
| AppealAttempts | int | Default: 0, >= 0 | Total number of real appeal attempts (wins + losses) |

### Relationships

- **AppealRecord** has no foreign key relationship to **UserReport**.
  They are independent entities that share a Discord user ID as a
  logical (not enforced) link.

### Identity & Uniqueness

- One `AppealRecord` per Discord user ID (enforced via unique index
  on `DiscordId`).
- If a user has never appealed, no `AppealRecord` row exists for
  them. The command handler treats a missing record as zero wins /
  zero attempts.

### Lifecycle

1. **Created**: On the user's first real appeal attempt (win or loss).
   Both counters initialized based on the outcome.
2. **Updated**: On each subsequent real appeal attempt. Counters are
   incremented atomically.
3. **Never deleted**: Appeal records are cumulative and permanent.
   No user command can delete or reset them.

### Derived Values (not stored)

- **Win Rate**: Calculated as `AppealWins / AppealAttempts * 100`
  at display time. Returns 0% if `AppealAttempts` is 0.

## Existing Entity Reference

### UserReport (unchanged)

| Field | Type | Description |
|-------|------|-------------|
| Id | int | PK, auto-increment |
| DiscordId | ulong | Indexed |
| DiscordName | string | Display name |
| InitiatedUserDiscordId | ulong | Who filed the report |
| InitiatedDiscordName | string | Reporter display name |
| Confused | bool | Self-report backfire flag |
| Description | string? | Report reason alias |

No changes to `UserReport`. Listed for context.
