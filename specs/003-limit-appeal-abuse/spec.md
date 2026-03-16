# Feature Specification: Limit Appeal Abuse

**Feature Branch**: `003-limit-appeal-abuse`
**Created**: 2026-03-12
**Status**: Draft
**Input**: User description: "Right now, the appeal system can be abused by running it multiple times. Instead it should only allow for one appeal per report, also if you report yourself, you should not be allowed to appeal it"

## Clarifications

### Session 2026-03-13

- Q: When the new "appealed" attribute is added, what should the default value be for existing reports? → A: Existing reports default to "appealed" (locked) — only new reports created after this change are eligible for appeal.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - One Appeal Per Report (Priority: P1)

A user who has been reported tries to appeal. The system selects a report that has not yet been appealed and processes the appeal (coin toss). Regardless of whether they win or lose, that specific report is marked as "appealed" and cannot be appealed again. If the user tries to appeal but all of their remaining reports have already been appealed, the system tells them they have no eligible reports to appeal.

**Why this priority**: This is the core fix — without it, users can spam `/appeal` to eventually remove all their reports, undermining the entire reporting system.

**Independent Test**: Can be fully tested by reporting a user, having them appeal once, then attempting a second appeal on the same report — the second attempt should be rejected.

**Acceptance Scenarios**:

1. **Given** a user has one unappealed report, **When** they run `/appeal`, **Then** the system processes the appeal (win or lose) and marks that report as appealed.
2. **Given** a user has one report that has already been appealed, **When** they run `/appeal`, **Then** the system responds that they have no eligible reports to appeal.
3. **Given** a user has three reports (two appealed, one unappealed), **When** they run `/appeal`, **Then** the system processes the appeal against the unappealed report only.
4. **Given** a user loses an appeal (penalty report added), **When** the penalty report is created, **Then** the penalty report is also marked as already appealed (cannot be appealed).

---

### User Story 2 - Block Self-Report Appeals (Priority: P1)

A user who reported themselves (either intentionally or via the backfire mechanic) attempts to appeal. The system recognizes that the report was self-initiated and rejects the appeal, informing the user that self-reports cannot be appealed.

**Why this priority**: Equally critical — self-reports are either intentional humor or a penalty mechanic (backfire). Allowing appeals on them defeats the purpose of both.

**Independent Test**: Can be tested by having a user self-report, then attempting to appeal — the system should reject the appeal with a clear message.

**Acceptance Scenarios**:

1. **Given** a user has only self-initiated reports, **When** they run `/appeal`, **Then** the system informs them that self-reports cannot be appealed.
2. **Given** a user has both self-initiated and other-initiated reports, **When** they run `/appeal`, **Then** the system only considers other-initiated, unappealed reports as eligible.
3. **Given** a user received a report via the backfire mechanic (self-report penalty), **When** they run `/appeal`, **Then** the backfire report is not eligible for appeal.

---

### User Story 3 - Appeal Feedback Clarity (Priority: P2)

When a user cannot appeal (no eligible reports), the system provides a clear and contextually appropriate message explaining why. The message should differentiate between "all reports already appealed" and "only self-reports remaining."

**Why this priority**: Good user experience — users should understand why their appeal was rejected rather than getting a generic error.

**Independent Test**: Can be tested by setting up each rejection scenario and verifying the response message matches the reason.

**Acceptance Scenarios**:

1. **Given** a user has reports but all have been appealed, **When** they run `/appeal`, **Then** the system responds with a message indicating all reports have already been appealed.
2. **Given** a user has reports but all are self-initiated, **When** they run `/appeal`, **Then** the system responds with a message indicating self-reports cannot be appealed.
3. **Given** a user has no reports at all, **When** they run `/appeal`, **Then** the existing penalty behavior (10 "DU" reports) still applies.

---

### Edge Cases

- What happens when a user has a mix of appealed, unappealed, self-reported, and other-reported reports? Only unappealed, other-initiated reports should be eligible.
- What happens when an appeal is won and the report is removed? The report is deleted, so the "appealed" flag is moot — the report no longer exists.
- What happens when the penalty report from a lost appeal is itself later targeted? Penalty reports from lost appeals should be pre-marked as appealed (not eligible).
- What about the existing "no reports" penalty (10 DU reports)? This behavior should remain unchanged — it is a separate path that does not interact with appeal eligibility.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST track whether each individual report has been appealed.
- **FR-002**: System MUST only select unappealed reports when processing an appeal.
- **FR-003**: System MUST mark a report as appealed after processing an appeal against it, regardless of outcome (win or lose).
- **FR-004**: System MUST exclude self-initiated reports (where the reporter and the reported are the same user) from appeal eligibility.
- **FR-005**: System MUST mark penalty reports (added from a lost appeal) as already appealed at creation time.
- **FR-006**: System MUST inform the user with a distinct message when no eligible reports exist because all reports have been appealed.
- **FR-007**: System MUST inform the user with a distinct message when no eligible reports exist because all remaining reports are self-initiated.
- **FR-008**: System MUST preserve existing behavior for users with zero reports (10 "DU" penalty reports).
- **FR-009**: System MUST continue to track overall appeal win/loss statistics as it does today.
- **FR-010**: System MUST treat all pre-existing reports (created before this feature) as already appealed — they are not eligible for appeal.

### Key Entities

- **UserReport**: Existing entity representing a report against a user. Gains a new attribute indicating whether it has been appealed.
- **AppealRecord**: Existing entity tracking aggregate appeal statistics per user. No changes expected.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A user can appeal each report at most once — repeated `/appeal` commands do not allow re-appealing the same report.
- **SC-002**: Self-initiated reports are never removed via the appeal system.
- **SC-003**: Users receive a contextually appropriate message when they have no eligible reports to appeal.
- **SC-004**: Existing appeal statistics (win count, attempt count, win rate) remain accurate and unaffected.
- **SC-005**: The "no reports" penalty path (10 DU reports) continues to function identically to current behavior.

## Assumptions

- A "self-report" is defined as any report where the reporter and the reported are the same user. This includes both intentional self-reports and backfire reports.
- When an appeal is won and the report is deleted, no additional tracking is needed for the deleted report.
- The appeal process remains a 50/50 coin toss — no changes to the probability mechanic.
- The appeal-count command continues to show aggregate stats and is not affected by per-report tracking.
- All pre-existing reports in the database are treated as already appealed (default value for the new attribute). This draws a clean line — only reports created after this change can be appealed.
