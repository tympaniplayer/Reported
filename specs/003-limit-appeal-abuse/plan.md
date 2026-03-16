# Implementation Plan: Limit Appeal Abuse

**Branch**: `003-limit-appeal-abuse` | **Date**: 2026-03-13 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/003-limit-appeal-abuse/spec.md`

## Summary

The appeal system currently allows unlimited appeals — users can spam `/appeal` to coin-flip remove all reports. This plan adds per-report appeal tracking (a `HasBeenAppealed` flag on `UserReport`) and excludes self-initiated reports from eligibility. The approach requires one new database column, one new enum, modifications to `AppealService`, and updated handler messages in `Program.cs`.

## Technical Context

**Language/Version**: C# / .NET 9.0 (nullable reference types enabled)
**Primary Dependencies**: Discord.Net 3.17.x, Entity Framework Core 9.0.3, CSharpFunctionalExtensions, Serilog
**Storage**: SQLite via EF Core (`ReportedDbContext`)
**Testing**: xUnit with in-memory SQLite (`TestDbContextFactory`), `FakeRandomProvider`
**Target Platform**: Linux (Docker / systemd on low-resource hardware)
**Project Type**: Single solution, two projects + test project
**Performance Goals**: N/A — single-user Discord commands, no concurrency concerns
**Constraints**: Must run on low-resource hardware; SQLite only
**Scale/Scope**: Small — ~150 active users, <10k reports

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Simplicity & Fun First | PASS | Feature is one sentence: "Each report can only be appealed once, and self-reports can't be appealed." No new abstractions. |
| I. YAGNI | PASS | No extension points or config layers added. |
| I. ≤2 new source files | PASS | One new file: `AppealRejectionReason.cs` enum. All other changes are modifications. |
| II. Separation of Concerns | PASS | Entity change in `Reported.Persistence`, business logic in `Reported/Services`, Discord handling in `Program.cs`. |
| II. No Discord types in persistence | PASS | `HasBeenAppealed` is a plain bool — no Discord types leak. |
| III. Discord API Compliance | PASS | No new commands; existing `/appeal` handler still calls `RespondAsync` exactly once. |
| IV. Observability | PASS | Existing Serilog logging in appeal handler covers new rejection paths. Will add structured log for rejection reason. |
| V. Data Integrity | PASS | Schema change via EF Core migration. Default value ensures existing data is safe. No destructive operations. |

**Post-Phase 1 re-check**: All gates still pass. One new file justified (enum type for rejection reasons). No new projects, no new abstractions.

## Project Structure

### Documentation (this feature)

```text
specs/003-limit-appeal-abuse/
├── plan.md              # This file
├── spec.md              # Feature specification
├── research.md          # Phase 0 research
├── data-model.md        # Phase 1 data model
├── quickstart.md        # Phase 1 quickstart
└── checklists/
    └── requirements.md  # Spec quality checklist
```

### Source Code (repository root)

```text
Reported/
├── Models/
│   ├── AppealOutcome.cs           # Modified: add RejectionReason field
│   └── AppealRejectionReason.cs   # NEW: enum (None, AllAppealed, OnlySelfReports)
├── Services/
│   └── AppealService.cs           # Modified: eligibility filter + rejection logic
└── Program.cs                     # Modified: rejection-specific messages

Reported.Persistence/
├── UserReport.cs                  # Modified: add HasBeenAppealed property
├── Configuration/
│   └── UserReportConfiguration.cs # Modified: HasDefaultValue(true)
└── Migrations/
    └── <timestamp>_AddHasBeenAppealedToUserReport.cs  # NEW: generated migration

Reported.Tests/
└── Services/
    └── AppealServiceTests.cs      # Modified: update + add tests
```

**Structure Decision**: Follows existing project layout exactly. The only new file is `AppealRejectionReason.cs` in `Models/` — justified because it's a distinct type used by both `AppealService` and `Program.cs`.

## Implementation Approach

### Step 1: Data Layer (Reported.Persistence)

1. Add `HasBeenAppealed` bool to `UserReport` with constructor default `false`
2. Add `HasDefaultValue(true)` in `UserReportConfiguration` (migration creates column with default `1` for existing rows)
3. Generate EF Core migration

### Step 2: New Types (Reported)

1. Create `AppealRejectionReason` enum: `None`, `AllAppealed`, `OnlySelfReports`
2. Add `RejectionReason` parameter to `AppealOutcome` record (default: `None`)

### Step 3: Service Logic (AppealService)

Update `ProcessAppeal`:
1. Query for eligible report: `!HasBeenAppealed && DiscordId != InitiatedUserDiscordId`
2. If no eligible report found, determine reason:
   - No reports at all → existing penalty path (unchanged)
   - Has reports but none eligible → check if any are non-self, unappealed → `AllAppealed` or `OnlySelfReports`
3. On appeal processed (win or lose): set `report.HasBeenAppealed = true`
4. On loss (penalty report created): set `HasBeenAppealed = true` on the new penalty report

### Step 4: Handler Updates (Program.cs)

Update `HandleAppeal` to check `RejectionReason` and display appropriate messages:
- `AllAppealed`: "You've already appealed all your reports."
- `OnlySelfReports`: "You can't appeal reports you gave yourself."

### Step 5: Tests

- Update existing tests to set `HasBeenAppealed = false` on seeded reports (since constructor default is `false`, this should work naturally)
- Add: appeal on already-appealed report → rejection
- Add: appeal on self-report only → rejection
- Add: mixed reports (some appealed, some self) → correct selection
- Add: penalty report from lost appeal is not re-appealable
- Add: rejection reason enum correctness

## Complexity Tracking

No constitution violations. No complexity justifications needed.
