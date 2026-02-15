# Feature Specification: Appeal Count

**Feature Branch**: `001-appeal-count`
**Created**: 2026-02-10
**Status**: Draft
**Input**: GitHub Issue #26 — "Add Appeal Count": Add /appeal-count command; Track amount of won appeals

## Clarifications

### Session 2026-02-10

- Q: Should `/appeal-count` responses be public or ephemeral? → A: Public (visible to everyone), consistent with all other bot commands.
- Q: Track only wins, or wins + total attempts? → A: Both wins and total attempts, enabling win-rate display and funnier messages.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - View My Appeal Wins (Priority: P1)

A Discord user who has successfully appealed reports in the past
wants to see how many appeals they have won. They type `/appeal-count`
in any channel where the bot is present and receive a message
showing their total number of successful appeals.

**Why this priority**: This is the core deliverable of the feature —
without the ability to view appeal wins, the feature has no user
value.

**Independent Test**: Can be fully tested by running `/appeal-count`
after winning at least one appeal and confirming the displayed count
matches the number of successful appeals.

**Acceptance Scenarios**:

1. **Given** a user has won 3 out of 10 appeals,
   **When** they run `/appeal-count`,
   **Then** they see a public message showing their wins, total
   attempts, and win rate (e.g., "3 wins out of 10 appeals — 30%").

2. **Given** a user has never appealed,
   **When** they run `/appeal-count`,
   **Then** they see a public message indicating zero attempts
   (or a fun message nudging them to try).

3. **Given** a user has never used the bot at all,
   **When** they run `/appeal-count`,
   **Then** they see a public message indicating zero attempts.

---

### User Story 2 - Appeal Wins Are Tracked Automatically (Priority: P1)

When a user wins an appeal via the existing `/appeal` command (the
50/50 coin flip results in approval), the system automatically
records that win so it is reflected in future `/appeal-count` queries.

**Why this priority**: Without tracking, there is nothing to display.
This is a prerequisite for User Story 1 and is equally critical.

**Independent Test**: Can be tested by winning an appeal and then
verifying the stored count incremented by one.

**Acceptance Scenarios**:

1. **Given** a user with 0 wins and 0 attempts,
   **When** they run `/appeal` and win,
   **Then** their win count increases to 1 and attempt count
   increases to 1.

2. **Given** a user with 5 wins and 8 attempts,
   **When** they run `/appeal` and lose,
   **Then** their win count remains at 5 and attempt count
   increases to 9.

3. **Given** a user with 0 reports who triggers the penalty
   (receiving 10 "Dumb" reports),
   **When** they run `/appeal`,
   **Then** neither their win count nor attempt count changes
   (the penalty is not a real appeal).

---

### Edge Cases

- What happens when the user runs `/appeal-count` in a DM vs. a
  guild channel? The command MUST work in both contexts, consistent
  with how other commands behave.
- What happens if the same user wins many appeals in rapid
  succession? Each win MUST be counted individually and accurately.
- What about historical appeals that were won before this feature
  existed? Those cannot be retroactively counted; the count starts
  from zero once the feature is deployed.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST provide a `/appeal-count` slash command
  accessible in guilds, private channels, and bot DMs.
- **FR-002**: The `/appeal-count` command MUST display the user's
  appeal wins, total attempts, and win rate as a public message.
- **FR-003**: The system MUST increment a user's win count and
  attempt count each time an appeal is approved (coin flip > 49).
- **FR-004**: The system MUST increment only the attempt count
  (not the win count) when an appeal is denied.
- **FR-004a**: The system MUST NOT increment either counter when the
  no-reports penalty is triggered (this is not a real appeal attempt).
- **FR-005**: The system MUST persist appeal win counts across bot
  restarts.
- **FR-006**: The `/appeal-count` response MUST be consistent with
  the bot's playful tone (e.g., fun phrasing, not dry statistics).

### Key Entities

- **Appeal Record**: Represents a user's cumulative appeal history.
  Key attributes: the Discord user identity, total number of won
  appeals, and total number of appeal attempts.

## Assumptions

- Appeal win tracking begins from zero at deployment time. There is
  no mechanism to reconstruct historical appeal wins from existing
  report deletion records.
- The `/appeal-count` command shows only the invoking user's own
  count. Viewing another user's appeal count is out of scope for
  this feature.
- The appeal win count is a simple cumulative counter (total wins
  ever), not a per-time-period metric.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: The bot's internal processing time for `/appeal-count`
  (from command receipt to response dispatch) MUST be under 100ms,
  verifiable via a lightweight benchmark against the data layer.
- **SC-002**: 100% of real appeal attempts (wins and losses) result
  in the attempt count being incremented by exactly 1, and 100% of
  wins additionally increment the win count by exactly 1.
- **SC-003**: Appeal win counts survive bot restarts without data
  loss.
- **SC-004**: The command is discoverable in Discord's slash command
  autocomplete alongside existing commands.
