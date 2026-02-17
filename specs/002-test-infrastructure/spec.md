# Feature Specification: Comprehensive Test Infrastructure

**Feature Branch**: `002-test-infrastructure`
**Created**: 2026-02-15
**Status**: Draft
**Input**: User description: "Get to work on GitHub issue #30 (unit test project for persistence layer), but not just for persistence layer. We need testing for the whole application, not just persistence."
**Related**: [GitHub Issue #30](https://github.com/tympaniplayer/Reported/issues/30)

## Clarifications

### Session 2026-02-15

- Q: How deep should the service extraction refactoring go? → A: Option B — Service classes with injected dependencies (DbContext, random abstraction) using a functional style with CSharpFunctionalExtensions (Result types, Maybe types, railway-oriented composition). No DI container; `Program.cs` manually creates instances. Services use `Result<T>` return types to make success/failure explicit and testable.
- Q: Should the spec explicitly define what's out of scope? → A: Yes — explicitly exclude Discord integration and E2E tests. Testing Discord API interactions (command registration, message formatting, bot lifecycle) is not part of this feature. A future refactor could make those testable separately.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Verify Data Operations Are Correct (Priority: P1)

As a developer, I want automated tests covering the persistence layer (database operations for reports and appeals) so that data operations are validated without requiring a live Discord connection.

**Why this priority**: The persistence layer is already cleanly isolated from Discord dependencies, making it the lowest-friction starting point. Data correctness is the foundation — broken data operations would affect every user-facing feature.

**Independent Test**: Can be fully tested by running `dotnet test` against an isolated in-memory database. Delivers confidence that report creation, appeal tracking, and statistics queries work correctly.

**Acceptance Scenarios**:

1. **Given** an empty database, **When** a user report is created with valid fields (reporter, target, reason), **Then** the report is persisted and retrievable with all fields intact
2. **Given** a database with existing reports, **When** reports are queried by target user, **Then** all reports for that user are returned with correct counts
3. **Given** no prior appeal record for a user, **When** an appeal is recorded, **Then** a new appeal record is created with attempt count of 1
4. **Given** an existing appeal record for a user, **When** another appeal is recorded, **Then** the existing record's attempt counter increments (not a duplicate row)
5. **Given** a user with an appeal record, **When** a winning appeal is recorded, **Then** both the win counter and attempt counter increment
6. **Given** a user with an appeal record, **When** a losing appeal is recorded, **Then** only the attempt counter increments (win counter unchanged)
7. **Given** two different users, **When** each creates an appeal record, **Then** the unique constraint allows both records to coexist independently

---

### User Story 2 - Verify Bot Business Logic Is Correct (Priority: P2)

As a developer, I want automated tests covering the bot's core business rules (report mechanics, appeal odds, critical hits, self-report penalties) so that game logic changes don't silently break existing behavior.

**Why this priority**: The business logic in `Program.cs` currently lives in static methods tightly coupled to Discord API types. Extracting this logic into functional-style service classes (using Result types for explicit success/failure) is the critical enabler for comprehensive testing. Without this, only the persistence layer can be tested.

**Independent Test**: Can be fully tested by running `dotnet test` against extracted service classes with controlled random number generation. Delivers confidence that report mechanics (critical hits, self-report penalties) and appeal outcomes behave correctly.

**Acceptance Scenarios**:

1. **Given** a user reports another user, **When** the report is processed, **Then** a report record is created targeting the correct user
2. **Given** a critical hit occurs (1% chance), **When** a report is processed, **Then** two reports are created instead of one
3. **Given** a self-report penalty triggers (5% chance), **When** a user tries to report someone, **Then** the report targets the initiator instead, marked as "confused"
4. **Given** a user appeals, **When** the appeal wins (50% chance), **Then** one of the user's reports is removed and their win count increments
5. **Given** a user appeals, **When** the appeal loses (50% chance), **Then** an additional report is added against them and only attempt count increments
6. **Given** a user with zero reports appeals, **When** the appeal wins, **Then** no report is removed (nothing to remove) and the win is still counted
7. **Given** controlled random outcomes, **When** business logic is executed, **Then** results are deterministic and verifiable

---

### User Story 3 - Verify Database Initialization Is Safe (Priority: P3)

As a developer, I want automated tests confirming that database initialization (migration application and legacy backfill) works correctly so that deployments to existing databases don't corrupt data.

**Why this priority**: Database initialization has been a source of production bugs (PRs #33 and #34 fixed issues with `EnsureCreated`/`MigrateAsync` conflicts and non-idempotent backfills). Automated tests prevent regression of these critical fixes.

**Independent Test**: Can be fully tested by running initialization against fresh and pre-existing databases. Delivers confidence that deployment won't break production data.

**Acceptance Scenarios**:

1. **Given** a completely new (empty) database, **When** initialization runs, **Then** all migrations apply successfully and all tables exist
2. **Given** a database where initialization has already run, **When** initialization runs again, **Then** no errors occur (idempotent behavior)
3. **Given** a legacy database (tables exist but no migration history), **When** initialization runs, **Then** migration history is backfilled and no duplicate table creation is attempted

---

### User Story 4 - Tests Run Automatically in CI (Priority: P4)

As a developer, I want tests to run automatically on every push and pull request so that breaking changes are caught before they reach production.

**Why this priority**: Automated CI test execution is the enforcement mechanism that makes tests valuable long-term. Without it, tests rot. However, tests must exist first (P1-P3) before CI integration matters.

**Independent Test**: Can be verified by pushing a commit and observing that the CI pipeline runs tests and reports results. Delivers confidence that the safety net is always active.

**Acceptance Scenarios**:

1. **Given** a pull request is opened, **When** the CI pipeline runs, **Then** all tests execute and results are reported on the PR
2. **Given** a test failure exists, **When** the CI pipeline runs, **Then** the pipeline fails and the failure is visible to the developer
3. **Given** all tests pass, **When** the CI pipeline runs, **Then** the pipeline succeeds and deployment proceeds normally

---

### Edge Cases

- What happens when the test database file is locked or unavailable? Tests should use isolated in-memory databases to avoid file contention.
- What happens when a test modifies shared state? Each test should get its own database instance to prevent cross-test contamination.
- What happens when a migration is added but tests aren't updated? Migration application tests should automatically validate all migrations without needing per-migration test updates.
- What happens when random-dependent logic is tested? Randomness must be injectable/controllable so tests produce deterministic results.

### Out of Scope

- **Discord integration testing**: Testing Discord API interactions (command registration, slash command responses, message formatting, bot lifecycle events) is not part of this feature. The Discord layer remains untested glue code in `Program.cs`.
- **End-to-end testing**: No tests that require a running Discord bot instance or a live Discord server connection.
- **Performance / load testing**: No stress tests or benchmarks beyond the 10-second suite completion target.
- **Discord handler refactoring**: While business logic is extracted into services, the Discord command handlers in `Program.cs` are not being restructured into a formal handler pattern. They become thinner but remain static methods.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The project MUST include a test project that can be executed via `dotnet test` from the solution root
- **FR-002**: Persistence tests MUST validate report creation, retrieval, and deletion operations
- **FR-003**: Persistence tests MUST validate appeal record upsert behavior (create-on-first, increment-on-subsequent)
- **FR-004**: Persistence tests MUST validate the unique constraint on appeal records (one per user)
- **FR-005**: Business logic MUST be extracted from `Program.cs` into service classes that accept dependencies (DbContext, random abstraction) via constructor, use functional Result types to communicate outcomes, and can be instantiated and tested without Discord dependencies. `Program.cs` remains the composition root that creates service instances manually (no DI container).
- **FR-006**: Business logic tests MUST validate report mechanics including critical hit (1% chance) and self-report penalty (5% chance) using controlled randomness
- **FR-007**: Business logic tests MUST validate appeal outcome logic (50% win/loss) and counter tracking
- **FR-008**: Database initialization tests MUST verify that migrations apply cleanly to a fresh database
- **FR-009**: Database initialization tests MUST verify idempotent behavior (running initialization twice causes no errors)
- **FR-010**: The CI pipeline MUST execute `dotnet test` and fail the build if any test fails
- **FR-011**: Each test MUST run in isolation with its own database instance to prevent cross-test contamination
- **FR-012**: Statistics query logic (who-reported, why-reported aggregations) MUST be testable through extracted services

### Assumptions

- xUnit is the test framework (industry standard for .NET, aligns with issue #30 suggestion)
- In-memory SQLite (`Data Source=:memory:`) is used for test databases, providing fast isolated tests without file system dependencies
- The existing bot behavior is correct as-is — tests codify current behavior, not desired behavior changes
- Refactoring `Program.cs` to extract services will not change any user-facing behavior
- The `Random` dependency will be abstracted to allow deterministic testing of probability-based features
- CSharpFunctionalExtensions is used for Result/Maybe types to make service method outcomes explicit and composable
- Service extraction follows a functional style: service methods return `Result<T>` rather than throwing exceptions or returning nulls, enabling railway-oriented test assertions

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: All persistence operations (report CRUD, appeal upsert, statistics queries) have at least one automated test each
- **SC-002**: All business logic rules (critical hit, self-report penalty, appeal win/loss) have at least one automated test each
- **SC-003**: Database initialization is tested for both fresh and existing database scenarios
- **SC-004**: All tests complete in under 10 seconds total (fast feedback loop)
- **SC-005**: Tests run automatically on every pull request with pass/fail results visible before merge
- **SC-006**: A developer can run all tests locally with a single command (`dotnet test`) and no external dependencies or configuration required
