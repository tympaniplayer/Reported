# Tasks: Comprehensive Test Infrastructure

**Input**: Design documents from `/specs/002-test-infrastructure/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/service-interfaces.md

**Tests**: Tests are the PRIMARY deliverable of this feature. Every user story is about testing.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create the test project, add dependencies, and make DbContext testable

- [X] T001 Create `Reported.Tests` xUnit project with NuGet packages (xunit, xunit.runner.visualstudio, Microsoft.NET.Test.Sdk, Microsoft.Data.Sqlite, Microsoft.EntityFrameworkCore.Sqlite) and project references to both `Reported` and `Reported.Persistence` in `Reported.Tests/Reported.Tests.csproj`. Add project to `Reported.sln`.
- [X] T002 [P] Add `CSharpFunctionalExtensions` NuGet package to `Reported/Reported.csproj`
- [X] T003 [P] Add a `DbContextOptions<ReportedDbContext>` constructor overload to `Reported.Persistence/ReportedDbContext.cs`. The new constructor accepts `DbContextOptions<ReportedDbContext>` and passes it to the base `DbContext(options)` constructor. The existing parameterless constructor remains unchanged for production use. Override `OnConfiguring` to only configure SQLite when options haven't been provided externally (check `optionsBuilder.IsConfigured`).

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Test helpers that ALL user stories depend on

**CRITICAL**: No user story work can begin until this phase is complete

- [X] T004 Create `TestDbContextFactory` in `Reported.Tests/Helpers/TestDbContextFactory.cs`. Static method `Create()` that: opens a `SqliteConnection("Data Source=:memory:")`, keeps it open, builds `DbContextOptions<ReportedDbContext>` with `UseSqlite(connection)`, creates a new `ReportedDbContext(options)`, calls `Database.EnsureCreated()`, and returns both the context and connection (so the connection can be disposed after the test). Consider returning a disposable wrapper or tuple.

**Checkpoint**: Foundation ready — user story implementation can now begin

---

## Phase 3: User Story 1 — Verify Data Operations Are Correct (Priority: P1) MVP

**Goal**: Automated tests covering persistence layer operations (report CRUD, appeal upsert, unique constraints, query accuracy)

**Independent Test**: Run `dotnet test --filter "FullyQualifiedName~Persistence"` — all persistence tests pass against in-memory SQLite

### Implementation for User Story 1

- [X] T005 [P] [US1] Write `UserReportTests` in `Reported.Tests/Persistence/UserReportTests.cs`. Test cases using `TestDbContextFactory.Create()`:
  - Create a UserReport with all fields and verify it persists and is retrievable with fields intact (FR-002)
  - Create multiple reports on the same DiscordId, query by DiscordId, verify correct count returned (spec scenario 2)
  - Create reports for different users, verify query isolation (only target user's reports returned)
  - Delete a report and verify it's removed (FR-002)
  - Verify the index on DiscordId exists (query by DiscordId returns results efficiently)

- [X] T006 [P] [US1] Write `AppealRecordTests` in `Reported.Tests/Persistence/AppealRecordTests.cs`. Test cases using `TestDbContextFactory.Create()`:
  - Create a new AppealRecord and verify it persists with AppealWins=0, AppealAttempts=0 (spec scenario 3)
  - Increment AppealAttempts on existing record and verify counter updates (spec scenario 4)
  - Increment both AppealWins and AppealAttempts, verify both update (spec scenario 5)
  - Increment only AppealAttempts (loss), verify AppealWins unchanged (spec scenario 6)
  - Create records for two different DiscordIds, verify both coexist (spec scenario 7, FR-004)
  - Attempt to create two AppealRecords with the same DiscordId, verify unique constraint violation throws (FR-004)

**Checkpoint**: Persistence layer fully tested. `dotnet test` passes. This is the MVP — deploy/demo ready.

---

## Phase 4: User Story 2 — Verify Bot Business Logic Is Correct (Priority: P2)

**Goal**: Extract business logic from `Program.cs` into functional-style service classes and test report mechanics (critical hit, self-report) and appeal logic (win/loss, no-reports penalty)

**Independent Test**: Run `dotnet test --filter "FullyQualifiedName~Services"` — all service tests pass with deterministic random outcomes

### Models and Abstractions for User Story 2

- [X] T007 [P] [US2] Create `IRandomProvider` interface in `Reported/Services/IRandomProvider.cs` with single method `int Next(int minValue, int maxValue)`. Create `RandomProvider` production implementation in `Reported/Services/RandomProvider.cs` that wraps a `System.Random` instance.
- [X] T008 [P] [US2] Create `FakeRandomProvider` in `Reported.Tests/Helpers/FakeRandomProvider.cs`. Accepts a sequence of return values via constructor (`params int[] returnValues`). Each call to `Next()` returns the next value in sequence. Throws if sequence is exhausted (helps catch unexpected extra random calls in tests).
- [X] T009 [P] [US2] Create value object records in `Reported/Models/`:
  - `ReportOutcome.cs`: `sealed record ReportOutcome(ulong TargetDiscordId, string TargetName, string ReasonCode, string ReasonDescription, int ReportCount, int TotalReportsOnTarget, int TotalReportsOfThisType, bool IsCriticalHit, bool IsSelfReport)`
  - `AppealOutcome.cs`: `sealed record AppealOutcome(bool Won, int AppealWins, int AppealAttempts, bool HadNoReports, int PenaltyReportsAdded)`
  - `ReportGroup.cs`: `sealed record ReportGroup(string GroupKey, string DisplayName, int Count)`
  - `AppealStats.cs`: `sealed record AppealStats(int Wins, int Attempts, int WinRate)`

### Service Implementation for User Story 2

- [X] T010 [US2] Implement `ReportingService` in `Reported/Services/ReportingService.cs`. Constructor takes `ReportedDbContext` and `IRandomProvider`. Methods per `contracts/service-interfaces.md`:
  - `CreateReport(...)` → `Task<Result<ReportOutcome>>`: Self-report check (random < 5 → 5%), critical hit check (random == 1 → 1%), create UserReport(s), query counts, return ReportOutcome. Use `Constants.ReportReasons` for reason description lookup.
  - `GetReportsByReporter(ulong)` → `Task<Result<IReadOnlyList<ReportGroup>>>`: Group UserReports by InitiatedDiscordName, return counts.
  - `GetReportsByReason(ulong)` → `Task<Result<IReadOnlyList<ReportGroup>>>`: Group UserReports by Description, map null/empty to "Unknown Reason", use Constants.ReportReasons for display names.
  - All methods return `Result.Failure<T>(error)` on exceptions, `Result.Success(value)` otherwise.

- [X] T011 [US2] Implement `AppealService` in `Reported/Services/AppealService.cs`. Constructor takes `ReportedDbContext` and `IRandomProvider`. Methods per `contracts/service-interfaces.md`:
  - `ProcessAppeal(ulong, string)` → `Task<Result<AppealOutcome>>`: Check for existing report. No reports → create 10 penalty reports with "DU" reason, return HadNoReports=true (do NOT create AppealRecord). Has reports → roll (random > 49 = win). Win: find/create AppealRecord, increment wins+attempts, remove matched report. Loss: find/create AppealRecord, increment attempts only, add 1 penalty report.
  - `GetAppealStats(ulong)` → `Task<Result<AppealStats>>`: Lookup AppealRecord, calculate win rate as `(int)Math.Round((double)wins / attempts * 100)`, return zeros if no record exists.
  - All methods return `Result.Failure<T>(error)` on exceptions, `Result.Success(value)` otherwise.

### Tests for User Story 2

- [X] T012 [P] [US2] Write `ReportingServiceTests` in `Reported.Tests/Services/ReportingServiceTests.cs`. Each test creates its own `TestDbContextFactory.Create()` and `FakeRandomProvider`. Test cases:
  - Normal report: FakeRandom returns [50, 50] (no self-report, no crit). Verify 1 report created on target, IsSelfReport=false, IsCriticalHit=false (spec scenario 1)
  - Critical hit: FakeRandom returns [50, 1] (no self-report, crit). Verify 2 reports created, IsCriticalHit=true (spec scenario 2)
  - Self-report backfire: FakeRandom returns [3] (triggers self-report). Verify 5 reports on initiator, IsSelfReport=true, Confused=true on all reports (spec scenario 3)
  - Statistics - GetReportsByReporter: Seed DB with reports from multiple initiators, verify correct grouping and counts
  - Statistics - GetReportsByReason: Seed DB with reports of different reasons, verify grouping. Include a report with null Description → "Unknown Reason" (FR-012)
  - Report counts: Create report, verify TotalReportsOnTarget and TotalReportsOfThisType reflect DB state after insert

- [X] T013 [P] [US2] Write `AppealServiceTests` in `Reported.Tests/Services/AppealServiceTests.cs`. Each test creates its own context and FakeRandomProvider. Test cases:
  - Appeal win: Seed 1 report, FakeRandom returns [50] (win). Verify Won=true, report removed, AppealWins=1, AppealAttempts=1 (spec scenario 4)
  - Appeal loss: Seed 1 report, FakeRandom returns [49] (loss). Verify Won=false, extra report added, AppealWins=0, AppealAttempts=1 (spec scenario 5)
  - No reports penalty: Empty DB, call ProcessAppeal. Verify HadNoReports=true, PenaltyReportsAdded=10, 10 "DU" reports in DB, NO AppealRecord created
  - Multiple appeals: Seed reports, win then lose. Verify cumulative AppealWins=1, AppealAttempts=2
  - GetAppealStats with no record: Verify returns Wins=0, Attempts=0, WinRate=0
  - GetAppealStats with record: Seed AppealRecord(wins=3, attempts=5), verify WinRate=60

### Program.cs Refactor for User Story 2

- [X] T014 [US2] Refactor `Reported/Program.cs` to delegate to services. For each command handler:
  - Replace `_random` static field with a `RandomProvider` instance created in `Main()`
  - `HandleReportCommand`: Create `ReportingService(dbContext, randomProvider)`, call `CreateReport(...)`, translate `Result<ReportOutcome>` to Discord response messages (preserve exact existing message format and emoji)
  - `HandleAppeal`: Create `AppealService(dbContext, randomProvider)`, call `ProcessAppeal(...)`, translate `Result<AppealOutcome>` to Discord responses (preserve exact existing messages and GIF link)
  - `HandleAppealCount`: Call `AppealService.GetAppealStats(...)`, translate `Result<AppealStats>` to the existing conditional response messages (never appealed / never won / low rate / normal)
  - `HandleWhoReportedCommand`: Call `ReportingService.GetReportsByReporter(...)`, translate to existing embed format
  - `HandleWhyReportedCommand`: Call `ReportingService.GetReportsByReason(...)`, translate to existing embed format
  - `HandleAliasListCommand`: No change needed (no business logic, just reads Constants)
  - **CRITICAL**: Preserve ALL existing Discord response messages, follow-up messages, embeds, and logging exactly. The refactor must be behavior-preserving. Run `dotnet build` to verify compilation.

**Checkpoint**: Business logic fully extracted and tested. All service tests pass with deterministic outcomes.

---

## Phase 5: User Story 3 — Verify Database Initialization Is Safe (Priority: P3)

**Goal**: Regression tests for database initialization covering fresh databases, idempotent re-runs, and legacy database migration backfill

**Independent Test**: Run `dotnet test --filter "FullyQualifiedName~DatabaseInit"` — all initialization tests pass

### Implementation for User Story 3

- [X] T015 [US3] Write `DatabaseInitTests` in `Reported.Tests/Persistence/DatabaseInitTests.cs`. These tests use temporary FILE-based SQLite databases (not in-memory) because `InitializeDatabaseAsync` manages its own connection internally. Each test creates a temp file path and cleans up after. Test cases:
  - Fresh database: Set `DATABASE_PATH` env var to temp file, call `InitializeDatabaseAsync()`, verify all tables exist (UserReport, AppealRecord, __EFMigrationsHistory) (FR-008, spec scenario 1)
  - Idempotent re-run: Run `InitializeDatabaseAsync()` twice on same database, verify no errors on second run (FR-009, spec scenario 2)
  - Legacy database backfill: Create a temp SQLite file, manually create UserReport table (mimicking pre-migration state without __EFMigrationsHistory), call `InitializeDatabaseAsync()`, verify migration history backfilled and AppealRecord table created without errors (spec scenario 3)
  - **IMPORTANT**: Save and restore the original `DATABASE_PATH` env var in test setup/teardown to avoid cross-test contamination. Consider using `IDisposable` pattern or xUnit's `IAsyncLifetime`.

**Checkpoint**: Database initialization regression-tested. Covers the exact bugs from PRs #33-34.

---

## Phase 6: User Story 4 — Tests Run Automatically in CI (Priority: P4)

**Goal**: Add `dotnet test` step to the GitHub Actions pipeline so tests run on every push and PR

**Independent Test**: Push a commit to a branch and verify the CI pipeline runs tests before building the Docker image

### Implementation for User Story 4

- [X] T016 [US4] Add a `dotnet test` step to `.github/workflows/deploy.yml`. Insert BEFORE the Docker build step in the `build-and-push` job. The step should:
  - Run `dotnet test --configuration Release --no-build` (or `dotnet test` if a separate build step is needed)
  - Fail the entire job if any test fails (default behavior)
  - Consider adding `--verbosity normal` for readable CI output
  - Ensure .NET SDK 9.0 is available (add `actions/setup-dotnet@v4` step if not already present)

**Checkpoint**: CI pipeline runs all tests. Failing tests block deployment.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Final validation and cleanup

- [X] T017 Run full test suite with `dotnet test` from solution root. Verify ALL tests pass and complete in under 10 seconds total (SC-004). If any test is slow, investigate and optimize.
- [X] T018 Verify `dotnet build` succeeds with zero warnings on both `Reported` and `Reported.Tests` projects
- [X] T019 Validate `specs/002-test-infrastructure/quickstart.md` instructions: run each command listed and verify it works as documented

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on T001 (test project must exist) — BLOCKS all user stories
- **US1 (Phase 3)**: Depends on Phase 2 — No dependencies on other stories
- **US2 (Phase 4)**: Depends on Phase 2 AND Phase 1 T002 (CSharpFunctionalExtensions) — No dependencies on US1
- **US3 (Phase 5)**: Depends on Phase 2 (T001 only) — Fully independent of US1 and US2
- **US4 (Phase 6)**: Depends on at least one test existing (any of US1/US2/US3)
- **Polish (Phase 7)**: Depends on all prior phases complete

### User Story Dependencies

- **US1 (P1)**: Can start after Phase 2 — Independent
- **US2 (P2)**: Can start after Phase 2 — Independent of US1 (but larger scope)
- **US3 (P3)**: Can start after Phase 2 — Fully independent
- **US4 (P4)**: Needs at least US1 complete to be meaningful

### Within User Story 2 (largest story)

- T007, T008, T009 can all run in parallel (different files, no dependencies)
- T010, T011 depend on T007 + T009 (services need IRandomProvider and value objects)
- T012, T013 depend on their respective services (T010/T011) + T008 (FakeRandomProvider)
- T014 depends on T010 + T011 (Program.cs needs services to exist)

### Parallel Opportunities

```text
Phase 1:  T001 ──→ T002 [P]
                ──→ T003 [P]

Phase 2:  T004 (sequential — depends on T001)

Phase 3:  T005 [P] ──┐
          T006 [P] ──┘ (can run in parallel)

Phase 4:  T007 [P] ──┐
          T008 [P] ──┤ (can run in parallel)
          T009 [P] ──┘
              ↓
          T010 ──┐
          T011 ──┘ (can run in parallel with each other)
              ↓
          T012 [P] ──┐
          T013 [P] ──┘ (can run in parallel)
              ↓
          T014 (sequential — needs both services)

Phase 5:  T015 (can run in parallel with Phase 4 tasks)

Phase 6:  T016 (after any tests exist)
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001-T003)
2. Complete Phase 2: Foundational (T004)
3. Complete Phase 3: US1 persistence tests (T005-T006)
4. **STOP and VALIDATE**: `dotnet test` passes, persistence layer verified
5. This alone closes the core ask of GitHub Issue #30

### Incremental Delivery

1. Setup + Foundational → Foundation ready
2. Add US1 (persistence tests) → `dotnet test` passes → **MVP complete** (Issue #30 core)
3. Add US2 (service extraction + tests) → Business logic verified → **Major milestone**
4. Add US3 (init tests) → Initialization regression-tested → **Safety net for deployments**
5. Add US4 (CI) → Tests enforced on every push → **Full feature complete**
6. Each story adds value without breaking previous stories

---

## Notes

- [P] tasks = different files, no dependencies between them
- [Story] label maps task to specific user story for traceability
- Each user story is independently completable and testable
- Commit after each task or logical group
- Stop at any checkpoint to validate the story independently
- The spec explicitly states tests ARE the deliverable — every task either creates tests or enables testing
- T014 (Program.cs refactor) is the highest-risk task — existing behavior must be preserved exactly
