# Slash Command Contracts: Appeal Count

**Feature**: 001-appeal-count
**Date**: 2026-02-10

## New Command: `/appeal-count`

### Registration

| Property | Value |
|----------|-------|
| Name | `appeal-count` |
| Description | `See how many appeals you've won` |
| Context Types | Guild, PrivateChannel, BotDm |
| Options | None |

### Behavior

**Input**: None (operates on the invoking user).

**Processing**:
1. Look up `AppealRecord` by `command.User.Id`.
2. If no record found: wins = 0, attempts = 0.
3. Calculate win rate: `wins / attempts * 100` (0% if attempts = 0).
4. Format a playful response message.

**Output** (public message via `RespondAsync`):

| Condition | Example Response |
|-----------|-----------------|
| Has wins and attempts | `"{mention}, you've won {wins} out of {attempts} appeals ({rate}%). Not bad... or is it?"` |
| Zero attempts | `"{mention}, you haven't even tried to appeal yet. Coward."` |
| Many attempts, few wins | `"{mention}, you've won {wins} out of {attempts} appeals ({rate}%). The system is clearly rigged."` |

*Exact wording is flexible — the tone MUST be playful and
consistent with existing bot personality.*

**Error handling**: No user-facing errors expected. Database
failures are logged via Serilog and the command responds with a
generic fallback message.

---

## Modified Command: `/appeal`

### Current Behavior (unchanged)

The 50/50 coin flip and report removal logic remain identical.

### Added Behavior

After a real appeal occurs (NOT the no-reports penalty branch):

| Outcome | Counter Updates |
|---------|----------------|
| Win (coinToss > 49) | Increment `AppealWins` + 1, `AppealAttempts` + 1 |
| Loss (coinToss <= 49) | Increment `AppealAttempts` + 1 only |
| No-reports penalty | No counter changes |

**Upsert logic**: If no `AppealRecord` exists for the user, create
one with initial values matching the outcome. If one exists,
increment the relevant counters.

Counter updates MUST be saved to the database in the same
`SaveChangesAsync` call as any report removal (for wins) to
ensure atomicity.
