# Tasks: Appeal Count

**Input**: Design documents from `/specs/001-appeal-count/`
**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md, contracts/

**Tests**: No test tasks included — tests were not requested in the feature specification.

**Organization**: Tasks are grouped by user story. US2 (tracking) is implemented before US1 (display) because tracking creates the data that the display command reads.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2)
- Include exact file paths in descriptions

## Path Conventions

- **Bot project**: `Reported/` (Discord interactions, command handlers)
- **Persistence project**: `Reported.Persistence/` (entities, EF config, migrations)

---

## Phase 1: Foundational (Shared Entity)

**Purpose**: Create the `AppealRecord` entity and database migration that both user stories depend on.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [x] T001 [P] Create `AppealRecord` entity class in `Reported.Persistence/AppealRecord.cs` with fields: `Id` (int, PK), `DiscordId` (ulong), `DiscordName` (string), `AppealWins` (int, default 0), `AppealAttempts` (int, default 0). Follow the `UserReport` POCO pattern with a primary constructor.
- [x] T002 [P] Create `AppealRecordConfiguration` in `Reported.Persistence/Configuration/AppealRecordConfiguration.cs` implementing `IEntityTypeConfiguration<AppealRecord>`. Configure: `HasKey(x => x.Id)`, `HasIndex(i => i.DiscordId).IsUnique()`. Follow the `UserReportConfiguration` pattern.
- [x] T003 Generate EF Core migration by running `dotnet ef migrations add AddAppealRecord --project Reported.Persistence` from repository root.
- [x] T004 Verify the solution builds cleanly by running `dotnet build` from repository root.

**Checkpoint**: `AppealRecord` entity exists with migration. Both stories can now proceed.

---

## Phase 2: User Story 2 — Appeal Wins Are Tracked Automatically (Priority: P1)

**Goal**: When a user wins or loses a real appeal via `/appeal`, the system records the outcome in `AppealRecord` counters.

**Independent Test**: Run `/appeal` multiple times. After each real appeal (not the no-reports penalty), verify the `AppealRecord` counters incremented correctly by later running `/appeal-count` (once US1 is done) or by inspecting the database directly.

### Implementation for User Story 2

- [x] T005 [US2] Modify `HandleAppeal` in `Reported/Program.cs` to add appeal tracking after the coin flip. Implement upsert logic: look up `AppealRecord` by `command.User.Id`; if none exists, create one. On win (`coinToss > 49`): increment both `AppealWins` and `AppealAttempts`, then save in the same `SaveChangesAsync` call as the report removal. On loss (`coinToss <= 49`): increment only `AppealAttempts` and save. Do NOT modify the no-reports penalty branch (the `report is null` case) — no counter changes there.
- [x] T006 [US2] Add structured Serilog logging in the appeal tracking code in `Reported/Program.cs`. Log appeal outcomes with properties: `{DiscordId}`, `{AppealOutcome}` ("won"/"lost"), `{TotalWins}`, `{TotalAttempts}`. Use `_logger.Information(...)` with structured properties, not string interpolation.

**Checkpoint**: Every real `/appeal` invocation now persists win/attempt data. The no-reports penalty does not affect counters.

---

## Phase 3: User Story 1 — View My Appeal Wins (Priority: P1) 🎯 MVP

**Goal**: Users can run `/appeal-count` to see their wins, total attempts, and win rate as a playful public message.

**Independent Test**: Run `/appeal-count` in a Discord channel. Verify the response is public, shows correct win/attempt/rate numbers, and has a fun tone.

### Implementation for User Story 1

- [x] T007 [US1] Add `AppealCountCommand()` static method in `Reported/Commands.cs` returning `SlashCommandProperties`. Name: `appeal-count`, description: `See how many appeals you've won`, context types: `Guild`, `PrivateChannel`, `BotDm`. No options. Follow the `AppealCommand()` pattern.
- [x] T008 [US1] Register the new command in `Program.cs` `ClientReady` handler by adding `await _client.CreateGlobalApplicationCommandAsync(Commands.AppealCountCommand());` alongside the existing command registrations.
- [x] T009 [US1] Add `case "appeal-count":` to the `SlashCommandHandler` switch in `Reported/Program.cs`, routing to a new `HandleAppealCount` method.
- [x] T010 [US1] Implement `HandleAppealCount` method in `Reported/Program.cs`. Logic: (1) start a `Stopwatch`, (2) look up `AppealRecord` by `command.User.Id`, (3) if null treat as 0 wins / 0 attempts, (4) calculate win rate (`wins / attempts * 100`, 0% if attempts = 0), (5) format a playful public response via `RespondAsync` with tone-varied messages based on the data (zero attempts, low win rate, decent win rate, etc.), (6) log elapsed time via Serilog with `{ElapsedMs}` structured property.

**Checkpoint**: `/appeal-count` is fully functional. Users see their wins, attempts, and win rate with playful messaging. Response is public.

---

## Phase 4: Polish & Cross-Cutting Concerns

**Purpose**: Build validation and quickstart walkthrough.

- [x] T011 Run `dotnet build` and verify zero errors or warnings.
- [ ] T012 Walk through `specs/001-appeal-count/quickstart.md` validation checklist to verify all acceptance criteria end-to-end.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Foundational (Phase 1)**: No dependencies — can start immediately
  - T001 and T002 can run in parallel (different files)
  - T003 depends on T001 + T002 (migration needs both entity and config)
  - T004 depends on T003 (build verification after migration)
- **User Story 2 (Phase 2)**: Depends on Phase 1 completion
  - T005 must complete before T006 (logging wraps the tracking code)
- **User Story 1 (Phase 3)**: Depends on Phase 1 completion (can technically start in parallel with US2, but US2 first is recommended for end-to-end testing)
  - T007, T008, T009 can run in parallel with each other (different methods/locations)
  - T010 depends on T009 (handler method called from the switch case)
- **Polish (Phase 4)**: Depends on Phase 2 + Phase 3 completion

### User Story Dependencies

- **User Story 2 (Tracking)**: Depends on Foundational phase only. No dependency on US1.
- **User Story 1 (Display)**: Depends on Foundational phase only. Works with zero data (no hard dependency on US2). However, implementing US2 first ensures meaningful test data exists.

### Recommended Execution Order

```
T001, T002 (parallel) → T003 → T004 → T005 → T006 → T007, T008, T009 (parallel) → T010 → T011 → T012
```

---

## Parallel Opportunities

### Phase 1: Foundational

```text
# These two tasks touch different files and can run in parallel:
T001: Create AppealRecord entity in Reported.Persistence/AppealRecord.cs
T002: Create AppealRecordConfiguration in Reported.Persistence/Configuration/AppealRecordConfiguration.cs
```

### Phase 3: User Story 1

```text
# These three tasks touch different methods/locations and can run in parallel:
T007: Add AppealCountCommand() in Reported/Commands.cs
T008: Register command in Program.cs ClientReady
T009: Add switch case in Program.cs SlashCommandHandler
```

---

## Implementation Strategy

### MVP (Recommended)

1. Complete Phase 1: Foundational (entity + migration)
2. Complete Phase 2: User Story 2 (tracking)
3. Complete Phase 3: User Story 1 (display)
4. **STOP and VALIDATE**: Run through quickstart.md checklist
5. Complete Phase 4: Polish

### All-at-Once (Single Developer)

Since this is a small feature (12 tasks), all phases can be completed in a single session:

1. T001 + T002 (parallel) → T003 → T004
2. T005 → T006
3. T007 + T008 + T009 (parallel) → T010
4. T011 → T012

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- No test tasks generated — tests were not requested in the spec
- Commit after each phase for clean git history
- The feature is small enough for a single developer to complete in one session
