# Tasks: Limit Appeal Abuse

**Input**: Design documents from `/specs/003-limit-appeal-abuse/`
**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md

**Tests**: Tests are included — existing test infrastructure is in place and existing tests need updating.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: No new project setup needed — this is a modification to existing code. This phase is empty.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Schema change and new types that ALL user stories depend on

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T001 Add `HasBeenAppealed` bool property to `UserReport` entity with constructor default `false` in `Reported.Persistence/UserReport.cs`
- [x] T002 Configure `HasDefaultValue(true)` for `HasBeenAppealed` in `Reported.Persistence/Configuration/UserReportConfiguration.cs`
- [x] T003 Generate EF Core migration via `dotnet ef migrations add AddHasBeenAppealedToUserReport --project Reported.Persistence --startup-project Reported.Persistence`
- [x] T004 [P] Create `AppealRejectionReason` enum (`None`, `AllAppealed`, `OnlySelfReports`) in `Reported/Models/AppealRejectionReason.cs`
- [x] T005 [P] Add `RejectionReason` parameter (type `AppealRejectionReason`, default `None`) to `AppealOutcome` record in `Reported/Models/AppealOutcome.cs`
- [x] T006 Verify `dotnet build` succeeds with all foundational changes

**Checkpoint**: Schema and types ready — user story implementation can now begin

---

## Phase 3: User Story 1 - One Appeal Per Report (Priority: P1) 🎯 MVP

**Goal**: Each report can only be appealed once. After an appeal (win or lose), the report is marked as appealed and cannot be targeted again. Penalty reports from lost appeals are pre-marked as appealed.

**Independent Test**: Report a user, appeal once (should succeed), attempt second appeal on same report (should be rejected with `AllAppealed` reason).

### Tests for User Story 1

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [x] T007 [US1] Add test: appeal succeeds on unappealed report, report is marked `HasBeenAppealed = true` after win in `Reported.Tests/Services/AppealServiceTests.cs`
- [x] T008 [US1] Add test: appeal succeeds on unappealed report, report is marked `HasBeenAppealed = true` after loss in `Reported.Tests/Services/AppealServiceTests.cs`
- [x] T009 [US1] Add test: appeal returns `AllAppealed` rejection when all reports are already appealed in `Reported.Tests/Services/AppealServiceTests.cs`
- [x] T010 [US1] Add test: with mixed appealed/unappealed reports, only unappealed report is selected in `Reported.Tests/Services/AppealServiceTests.cs`
- [x] T011 [US1] Add test: penalty report from lost appeal is created with `HasBeenAppealed = true` in `Reported.Tests/Services/AppealServiceTests.cs`
- [x] T012 [US1] Update existing `ProcessAppeal_Win_RemovesReportAndTracksStats` test — seeded report should have `HasBeenAppealed = false` (verify constructor default works) in `Reported.Tests/Services/AppealServiceTests.cs`
- [x] T013 [US1] Update existing `ProcessAppeal_NoReports_AddsTenPenaltyReports` test — verify the 10 DU penalty reports are self-reports (unchanged behavior) in `Reported.Tests/Services/AppealServiceTests.cs`

### Implementation for User Story 1

- [x] T014 [US1] Update `ProcessAppeal` in `Reported/Services/AppealService.cs`: change report query from `FirstOrDefaultAsync(r => r.DiscordId == userDiscordId)` to filter by `!HasBeenAppealed` (and `DiscordId != InitiatedUserDiscordId` — sets up US2 for free)
- [x] T015 [US1] Update `ProcessAppeal` in `Reported/Services/AppealService.cs`: when no eligible report found but user has reports, determine rejection reason (`AllAppealed` vs `OnlySelfReports`) and return `AppealOutcome` with populated `RejectionReason`
- [x] T016 [US1] Update `ProcessAppeal` in `Reported/Services/AppealService.cs`: after coin toss (win or lose), set `report.HasBeenAppealed = true` before `SaveChangesAsync`
- [x] T017 [US1] Update `ProcessAppeal` in `Reported/Services/AppealService.cs`: when creating penalty report on loss, set `HasBeenAppealed = true` on the new `UserReport`
- [x] T018 [US1] Add structured Serilog log for appeal rejection with `{RejectionReason}` property in `Reported/Program.cs` `HandleAppeal` method
- [x] T019 [US1] Run tests and verify all US1 tests pass via `dotnet test`

**Checkpoint**: Per-report appeal limiting is fully functional. Users can appeal each report exactly once.

---

## Phase 4: User Story 2 - Block Self-Report Appeals (Priority: P1)

**Goal**: Self-initiated reports (including backfire reports) are excluded from appeal eligibility. If a user only has self-reports, they get a specific rejection.

**Independent Test**: Create a self-report (where reporter = reported), attempt appeal — should be rejected with `OnlySelfReports` reason.

**Note**: The core query filter (`DiscordId != InitiatedUserDiscordId`) was already added in T014 as part of the eligibility query. This phase adds tests and verifies the behavior.

### Tests for User Story 2

- [x] T020 [US2] Add test: user with only self-reports gets `OnlySelfReports` rejection in `Reported.Tests/Services/AppealServiceTests.cs`
- [x] T021 [US2] Add test: user with mix of self-reports and other-reports — only other-initiated, unappealed report is selected in `Reported.Tests/Services/AppealServiceTests.cs`
- [x] T022 [US2] Add test: backfire report (self-report via `Confused = true`) is not eligible for appeal in `Reported.Tests/Services/AppealServiceTests.cs`
- [x] T023 [US2] Run tests and verify all US1 + US2 tests pass via `dotnet test`

**Checkpoint**: Self-report exclusion is verified. Both P1 stories are complete.

---

## Phase 5: User Story 3 - Appeal Feedback Clarity (Priority: P2)

**Goal**: Users receive clear, contextually appropriate rejection messages distinguishing "all reports appealed" from "only self-reports remaining."

**Independent Test**: Set up each rejection scenario and verify the Discord response message matches the reason.

### Implementation for User Story 3

- [x] T024 [US3] Update `HandleAppeal` in `Reported/Program.cs`: add check for `outcome.RejectionReason` before existing win/loss logic, with distinct `RespondAsync` message for `AllAppealed` (e.g., "You've already appealed all your reports")
- [x] T025 [US3] Update `HandleAppeal` in `Reported/Program.cs`: add distinct `RespondAsync` message for `OnlySelfReports` (e.g., "You can't appeal reports you gave yourself")
- [x] T026 [US3] Verify existing "no reports" penalty path still shows existing message (unchanged) in `Reported/Program.cs`
- [x] T027 [US3] Run full test suite via `dotnet test` and verify all tests pass

**Checkpoint**: All user stories complete. Users see appropriate messages for every appeal scenario.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final verification across all stories

- [x] T028 Run `dotnet build` with zero warnings
- [x] T029 Run full `dotnet test` suite — all existing and new tests must pass
- [x] T030 Verify migration applies cleanly on a fresh database via quickstart.md validation

---

## Dependencies & Execution Order

### Phase Dependencies

- **Foundational (Phase 2)**: No dependencies - start immediately. T001 → T002 → T003 (sequential, schema changes). T004 and T005 can run in parallel with T001-T003.
- **User Story 1 (Phase 3)**: Depends on Phase 2 completion. Tests first (T007-T013), then implementation (T014-T019).
- **User Story 2 (Phase 4)**: Depends on Phase 3 (T014 adds the self-report filter). This phase is primarily test verification.
- **User Story 3 (Phase 5)**: Depends on Phase 3 (needs `RejectionReason` in `AppealOutcome`). Can run in parallel with Phase 4.
- **Polish (Phase 6)**: Depends on all stories being complete.

### User Story Dependencies

- **User Story 1 (P1)**: Core implementation — all other stories depend on it
- **User Story 2 (P1)**: Depends on US1 (query filter added in T014). Adds test coverage only.
- **User Story 3 (P2)**: Depends on US1 (uses `RejectionReason`). Independent of US2.

### Parallel Opportunities

- T004 and T005 can run in parallel with T001-T003 (different files)
- T024 and T025 can be done in a single pass (same file, same method)
- US2 (Phase 4) and US3 (Phase 5) can run in parallel after US1 completes

---

## Parallel Example: Foundational Phase

```bash
# Sequential (schema dependencies):
Task T001: "Add HasBeenAppealed to UserReport"
Task T002: "Configure HasDefaultValue in UserReportConfiguration" (depends on T001)
Task T003: "Generate migration" (depends on T002)

# Parallel with above (different projects):
Task T004: "Create AppealRejectionReason enum"
Task T005: "Extend AppealOutcome record"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 2: Foundational (T001-T006)
2. Complete Phase 3: User Story 1 (T007-T019)
3. **STOP and VALIDATE**: Appeal each report at most once — core abuse prevention is live
4. Deploy if ready — this alone solves the primary problem

### Incremental Delivery

1. Foundational → Foundation ready
2. User Story 1 → Test → Deploy (MVP! Abuse prevention active)
3. User Story 2 → Test → Deploy (Self-report protection added)
4. User Story 3 → Test → Deploy (Clear rejection messages)
5. Each story adds value without breaking previous stories

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Existing tests in `AppealServiceTests.cs` need minor updates (T012, T013) — constructor default of `false` means seeded reports are automatically appealable, so most existing tests should pass without changes
- The self-report filter in T014 serves both US1 and US2 — this is intentional to avoid touching the query twice
- Commit after each phase completion for clean git history
