# Research: Limit Appeal Abuse

**Branch**: `003-limit-appeal-abuse` | **Date**: 2026-03-13

## R1: How to add a boolean column with default `true` for existing rows in EF Core + SQLite

- **Decision**: Add `HasBeenAppealed` property to `UserReport` with a migration that uses `defaultValue: true`. SQLite applies the default to all existing rows.
- **Rationale**: EF Core's `HasDefaultValue(true)` in Fluent API generates the correct `ALTER TABLE ADD COLUMN ... DEFAULT 1` for SQLite. Existing rows automatically get `true` (locked). New code sets `false` explicitly on creation.
- **Alternatives considered**:
  - Separate migration step with `UPDATE` SQL: Unnecessary — SQLite `ALTER TABLE ADD COLUMN DEFAULT` applies to existing rows automatically.
  - Nullable bool with null = unappealed: Adds ambiguity; a non-nullable bool with explicit default is cleaner.

## R2: How to distinguish appeal rejection reasons in the existing Result<AppealOutcome> pattern

- **Decision**: Extend `AppealOutcome` record with a new `RejectionReason` enum field. When no eligible report exists, return a success result with `Won = false` and a populated `RejectionReason`.
- **Rationale**: Keeps the existing `Result.Success`/`Result.Failure` semantics intact — `Failure` is for unexpected errors, while "no eligible reports" is a valid business outcome. The handler in `Program.cs` switches on the reason to display different messages.
- **Alternatives considered**:
  - Return `Result.Failure` with a string message: Conflates business logic with error handling; harder to test.
  - Separate method `CanAppeal()`: Extra DB round-trip; better to check eligibility inline.
  - Multiple return types: C# doesn't support union types natively; enum is idiomatic.

## R3: Self-report detection reliability

- **Decision**: Use `DiscordId == InitiatedUserDiscordId` to detect self-reports.
- **Rationale**: Both `ReportingService.CreateReport` (backfire path, lines 28-33) and `AppealService.ProcessAppeal` (no-reports penalty, lines 31-33) set both IDs to the same user when creating self-reports. The `Confused` flag also indicates self-reports but is not set in all self-report paths (the "no reports" penalty sets `Confused = true` via the `confused` param, but for consistency, the ID comparison is the canonical check).
- **Alternatives considered**:
  - Use `Confused` flag: Not reliable — it's true for backfire reports but also for the "no reports" penalty DU reports. The ID comparison is more precise and universal.

## R4: New report creation — where to set `HasBeenAppealed = false`

- **Decision**: Set `HasBeenAppealed = false` in the `UserReport` constructor default, and set `true` explicitly only when creating penalty reports from lost appeals. The migration default of `true` covers existing rows.
- **Rationale**: The constructor default of `false` means all new reports (from `/report` command) are automatically appealable. Only two code paths need explicit `true`: (1) lost-appeal penalty reports and (2) the "no reports" penalty (DU) reports — but DU reports are self-reports and thus already ineligible via the self-report filter, so marking them is belt-and-suspenders.
- **Alternatives considered**:
  - Default constructor to `true` and set `false` in `ReportingService`: More change sites, more risk of missing one.
