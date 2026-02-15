# Research: Appeal Count

**Feature**: 001-appeal-count
**Date**: 2026-02-10

## Research Tasks

### 1. Data Storage Approach for Appeal Counters

**Decision**: Use a dedicated `AppealRecord` entity with cumulative
counters (wins + attempts) keyed by Discord user ID.

**Rationale**: The existing `UserReport` entity represents individual
report rows (one row per report). Appeal tracking is fundamentally
different — it's a per-user aggregate counter, not a per-event record.
A separate entity avoids polluting the report model with unrelated
fields and follows the Separation of Concerns principle.

**Alternatives considered**:
- *Add columns to `UserReport`*: Rejected — `UserReport` represents
  individual reports, not user-level aggregates. Adding appeal fields
  would conflate two different concerns. Also, a user can have many
  `UserReport` rows, so there's no single row to store the count on.
- *Count appeal events from a log/event table*: Rejected — YAGNI.
  A simple counter is sufficient. An event-sourced approach adds
  complexity with no benefit for this use case.
- *In-memory dictionary*: Rejected — violates FR-005 (persistence
  across restarts).

### 2. EF Core Entity Pattern

**Decision**: Follow the existing pattern — POCO class +
`IEntityTypeConfiguration<T>` in the `Configuration/` directory.
The `DbContext` already uses `ApplyConfigurationsFromAssembly`
which will auto-discover the new configuration.

**Rationale**: Consistency with `UserReport` + `UserReportConfiguration`.
No changes to `ReportedDbContext.cs` are needed beyond the migration.

**Alternatives considered**:
- *Data annotations on the entity*: Rejected — existing codebase
  uses fluent configuration exclusively.
- *Explicit `DbSet<AppealRecord>` property on DbContext*: Optional
  but not required — the codebase uses `dbContext.Set<T>()` pattern.
  Keeping this consistent avoids unnecessary DbContext changes.

### 3. Command Registration Pattern

**Decision**: Add a static `AppealCountCommand()` method to
`Commands.cs` returning `SlashCommandProperties`, following the
exact pattern of `AppealCommand()`, `WhoReportedCommand()`, etc.

**Rationale**: Direct consistency with existing code. The command
takes no parameters (like `who-reported` and `why-reported`).

### 4. Counter Increment Strategy

**Decision**: Use "upsert" semantics — when a real appeal occurs,
look up the `AppealRecord` by Discord ID. If it exists, increment
the relevant counters. If not, create a new record with initial
values.

**Rationale**: Handles the cold-start case (first appeal after
deployment) without requiring a separate initialization step.
The existing `HandleAppeal` method already does a lookup by
Discord ID, so the pattern is familiar.

### 5. Performance Benchmark Approach

**Decision**: Use Serilog structured logging with timing around the
database query in the `/appeal-count` handler. Log the elapsed
milliseconds as a structured property. This satisfies SC-001
without adding a test framework dependency.

**Rationale**: The bot already has Serilog + Axiom integration.
Logging query time as a structured property (e.g.,
`_logger.Information("Appeal count query completed in {ElapsedMs}ms", elapsed)`)
makes it queryable in Axiom and visible in console logs. No
separate benchmark harness needed for this simple query.

**Alternatives considered**:
- *BenchmarkDotNet*: Rejected — overkill for a single SQLite
  lookup. Adds a dev dependency for minimal value.
- *Unit test with stopwatch*: Rejected — would require mocking
  the DbContext or an in-memory database, adding test
  infrastructure the project doesn't currently have.
