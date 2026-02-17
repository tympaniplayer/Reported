# Implementation Plan: Comprehensive Test Infrastructure

**Branch**: `002-test-infrastructure` | **Date**: 2026-02-15 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/002-test-infrastructure/spec.md`

## Summary

Add comprehensive test infrastructure to the Reported bot by (1) creating an xUnit test project, (2) extracting business logic from the monolithic `Program.cs` into functional-style service classes using CSharpFunctionalExtensions `Result<T>` types, and (3) integrating `dotnet test` into the CI pipeline. The persistence layer is tested directly against in-memory SQLite. Business logic (report mechanics, appeal processing, statistics) is tested through extracted `ReportingService` and `AppealService` classes with injected dependencies.

## Technical Context

**Language/Version**: C# / .NET 9.0 (nullable reference types enabled)
**Primary Dependencies**: Discord.Net 3.17.x, Entity Framework Core 9.0.3, Serilog, CSharpFunctionalExtensions (new)
**Storage**: SQLite via EF Core (existing); in-memory SQLite for tests
**Testing**: xUnit (new), Microsoft.Data.Sqlite for in-memory test databases
**Target Platform**: Linux server (Docker), macOS (development)
**Project Type**: Multi-project .NET solution (console app + class library + test project)
**Performance Goals**: Test suite completes in under 10 seconds
**Constraints**: No external dependencies for test execution (no Discord, no Axiom, no network)
**Scale/Scope**: ~1000 LOC existing codebase, adding ~500-800 LOC of services + tests

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### Pre-Research Gate

| Principle | Status | Notes |
| --------- | ------ | ----- |
| I. Simplicity & Fun First | **VIOLATION — justified** | Feature adds more than 2 new source files (services, models, test files). Justified: test infrastructure inherently requires new files; service extraction is the minimum viable change to enable testability. See Complexity Tracking. |
| II. Separation of Concerns | **PASS** | Service extraction strengthens the boundary between bot logic and persistence. Services live in `Reported` project; no Discord types in persistence. |
| III. Discord API Compliance | **PASS** | No changes to Discord interactions. Command handlers still call `RespondAsync` exactly once. |
| IV. Observability | **PASS** | Services accept `ILogger` where needed. Existing Serilog structured logging preserved. |
| V. Data Integrity | **PASS** | No schema changes. Tests validate data operations. `MigrateAsync` still used in production; `EnsureCreated` only in test isolation. |

### Post-Design Gate

| Principle | Status | Notes |
| --------- | ------ | ----- |
| I. Simplicity & Fun First | **PASS (justified)** | New files: 4 service/model files in `Reported`, 1 minor change to `Reported.Persistence`, ~8 test files in `Reported.Tests`. Each is necessary — no speculative abstractions. YAGNI respected: no DI container, no repository pattern, no interface-per-service. |
| II. Separation of Concerns | **PASS** | `ReportingService` and `AppealService` cleanly separate game logic from Discord API glue. `Program.cs` becomes a thin adapter that constructs services and translates `Result<T>` to Discord responses. |
| III. Discord API Compliance | **PASS** | Unchanged — handlers still own the `RespondAsync` / `FollowupAsync` calls. |
| IV. Observability | **PASS** | Services log via Serilog structured properties. Test assertions can verify log output if needed. |
| V. Data Integrity | **PASS** | Database initialization tests specifically regression-test the idempotent backfill behavior from PRs #33-34. |

## Project Structure

### Documentation (this feature)

```text
specs/002-test-infrastructure/
├── plan.md              # This file
├── spec.md              # Feature specification
├── research.md          # Phase 0: technology decisions
├── data-model.md        # Phase 1: entity + service model
├── quickstart.md        # Phase 1: developer guide
├── contracts/           # Phase 1: service interfaces
│   └── service-interfaces.md
├── checklists/
│   └── requirements.md  # Spec quality checklist
└── tasks.md             # Phase 2 output (created by /speckit.tasks)
```

### Source Code (repository root)

```text
Reported/                              # Main bot project
├── Program.cs                         # MODIFIED — thinner, delegates to services
├── Commands.cs                        # Unchanged
├── Constants.cs                       # Unchanged
├── Services/                          # NEW — extracted business logic
│   ├── IRandomProvider.cs             # Random abstraction interface
│   ├── RandomProvider.cs              # Production System.Random wrapper
│   ├── ReportingService.cs            # Report creation + statistics queries
│   └── AppealService.cs              # Appeal processing + appeal stats
├── Models/                            # NEW — Result value types
│   ├── ReportOutcome.cs               # Report operation result record
│   ├── AppealOutcome.cs               # Appeal operation result record
│   ├── ReportGroup.cs                 # Statistics aggregation record
│   └── AppealStats.cs                # Appeal statistics record
├── External/
│   └── AxiomHttpClient.cs            # Unchanged
└── Reported.csproj                    # MODIFIED — add CSharpFunctionalExtensions

Reported.Persistence/                  # Data layer
├── ReportedDbContext.cs               # MODIFIED — add DbContextOptions overload
├── UserReport.cs                      # Unchanged
├── AppealRecord.cs                    # Unchanged
├── Configuration/                     # Unchanged
└── Reported.Persistence.csproj        # Unchanged

Reported.Tests/                        # NEW — test project
├── Reported.Tests.csproj              # xUnit + project references
├── Helpers/
│   ├── TestDbContextFactory.cs        # In-memory SQLite context factory
│   └── FakeRandomProvider.cs          # Deterministic random for tests
├── Persistence/
│   ├── UserReportTests.cs             # Report CRUD operations
│   ├── AppealRecordTests.cs           # Appeal upsert + unique constraint
│   └── DatabaseInitTests.cs           # Migration + initialization regression
└── Services/
    ├── ReportingServiceTests.cs       # Report mechanics (crits, self-reports, stats)
    └── AppealServiceTests.cs          # Appeal logic (win/loss, no-reports, stats)

.github/workflows/
└── deploy.yml                         # MODIFIED — add dotnet test step before Docker build
```

**Structure Decision**: Follows the existing .NET solution convention with a third project for tests. Services and models are organized under the `Reported` project (where business logic belongs per Constitution Principle II). No separate `Reported.Services` project — that would violate YAGNI given the codebase size.

## Complexity Tracking

> Constitution Principle I violation: more than 2 new source files

| Violation | Why Needed | Simpler Alternative Rejected Because |
| --------- | ---------- | ------------------------------------ |
| 4 new service/model files in `Reported/` | Service extraction is the minimum change to decouple business logic from Discord types for testability | Fewer files would mean stuffing multiple unrelated types into one file, reducing readability. Each file is small (< 50 LOC for models, ~100-150 LOC for services). |
| 8 new files in `Reported.Tests/` | Test files cannot share files with production code. Each test class covers a distinct concern (persistence entities, service logic, initialization). | Fewer test files would create large monolithic test classes mixing persistence and service tests, making failures harder to diagnose. |
| `CSharpFunctionalExtensions` NuGet dependency | Provides `Result<T>` / `Maybe<T>` for explicit success/failure without exceptions. User-requested functional style. | Without it, services would return tuples or throw exceptions — both worse for testability and readability. |
| New `Reported.Tests` project (3rd project in solution) | Test code must be in a separate project to avoid shipping test dependencies in production. This is standard .NET practice, not optional. | The only alternative is no tests, which defeats the feature's purpose. |
