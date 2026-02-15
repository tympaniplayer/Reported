# Implementation Plan: Appeal Count

**Branch**: `001-appeal-count` | **Date**: 2026-02-10 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/001-appeal-count/spec.md`

## Summary

Add a `/appeal-count` slash command that displays a user's appeal
wins, total attempts, and win rate. The existing `/appeal` command
handler must be modified to track both wins and attempts whenever a
real appeal occurs (excluding the no-reports penalty). Data is
persisted via a new EF Core entity in the `Reported.Persistence`
project.

## Technical Context

**Language/Version**: C# / .NET 9.0 (nullable reference types enabled)
**Primary Dependencies**: Discord.Net 3.17.x, Entity Framework Core 9.0.3, Serilog
**Storage**: SQLite via EF Core (existing `ReportedDbContext`)
**Testing**: Manual Discord testing; SC-001 benchmark via Serilog timing logs
**Target Platform**: Linux (Docker container or systemd service)
**Project Type**: Multi-project .NET solution (Reported + Reported.Persistence)
**Performance Goals**: <100ms internal processing time for `/appeal-count`
**Constraints**: Must run on low-resource hardware (Raspberry Pi / small VPS)
**Scale/Scope**: Small friend-group bot; single-digit concurrent users

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Simplicity & Fun First | PASS | Feature adds 1 new entity + 1 new command handler + modifications to 1 existing handler. Two new source files in persistence (entity + config), minimal changes in bot project. Justified: entity must live in persistence layer per Principle II. |
| II. Separation of Concerns | PASS | New `AppealRecord` entity and its configuration go in `Reported.Persistence`. Command handling and business logic stay in `Reported/Program.cs` and `Reported/Commands.cs`. No Discord types cross into persistence. |
| III. Discord API Compliance | PASS | New `/appeal-count` handler will call `RespondAsync` exactly once. Command registered via `Commands.cs` pattern. |
| IV. Observability | PASS | Appeal count queries and tracking increments will be logged via Serilog with structured properties. |
| V. Data Integrity | PASS | Schema change via EF Core migration. Counter increments are additive only — no destructive operations from user commands. |

**New source files justification** (Principle I, >2 file rule):
This feature touches existing files (`Program.cs`, `Commands.cs`) and
adds 2 new files in `Reported.Persistence` (`AppealRecord.cs`,
`AppealRecordConfiguration.cs`). The new files are required by
Principle II (entity + config must live in the persistence project).
Total new files = 2, which is within the limit.

## Project Structure

### Documentation (this feature)

```text
specs/001-appeal-count/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   └── commands.md      # Slash command contracts
└── tasks.md             # Phase 2 output (/speckit.tasks)
```

### Source Code (repository root)

```text
Reported/
├── Program.cs           # MODIFY: add HandleAppealCount, update HandleAppeal
├── Commands.cs          # MODIFY: add AppealCountCommand()

Reported.Persistence/
├── AppealRecord.cs              # NEW: entity class
├── Configuration/
│   └── AppealRecordConfiguration.cs  # NEW: EF fluent config
└── Migrations/
    └── <timestamp>_AddAppealRecord.cs  # NEW: auto-generated migration
```

**Structure Decision**: Follows the existing two-project layout.
No new projects or directories needed. The new entity follows the
same pattern as `UserReport` + `UserReportConfiguration`.

## Complexity Tracking

> No constitution violations. This section is intentionally empty.
