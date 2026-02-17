# Research: Comprehensive Test Infrastructure

**Branch**: `002-test-infrastructure` | **Date**: 2026-02-15

## R-001: Service Extraction Approach

**Decision**: Extract business logic into service classes with constructor-injected dependencies, using CSharpFunctionalExtensions `Result<T>` return types. No DI container — `Program.cs` manually constructs service instances.

**Rationale**: The current `Program.cs` is a 356-line monolith with all business logic in static methods tightly coupled to `SocketSlashCommand` (Discord.Net). Extracting into services decouples game logic from Discord API types, making it testable. CSharpFunctionalExtensions provides `Result<T>` and `Maybe<T>` types for explicit success/failure without exceptions, which improves both testability and code clarity.

**Alternatives considered**:
- **Static helper methods**: Simplest extraction but can't inject dependencies (DbContext, random), limiting test isolation
- **Full DI container**: `Microsoft.Extensions.DependencyInjection` with interfaces for everything — over-engineered for a ~1000 LOC bot with 2 services
- **Mediator pattern (MediatR)**: Way too heavy for 6 commands; adds unnecessary indirection

## R-002: Service Boundaries

**Decision**: Two service classes — `ReportingService` (report creation + statistics) and `AppealService` (appeal processing + appeal statistics). Both accept `ReportedDbContext` and an `IRandomProvider` abstraction via constructor.

**Rationale**: The bot has two clear domains: reporting (create reports, query statistics) and appeals (process appeals, track win/loss). Grouping statistics with their domain keeps related logic together. A separate `StatisticsService` was considered but rejected as over-separation — the statistics queries are trivial (GroupBy + Count) and tightly coupled to their domain entities.

**Alternatives considered**:
- **Three services** (Reporting + Appeal + Statistics): Over-separation for this codebase size; statistics queries are 5-10 lines each
- **Single GameService**: Under-separation; report and appeal logic are independent domains
- **Repository pattern**: Unnecessary abstraction over EF Core for this project size; services query DbContext directly via `dbContext.Set<T>()`

## R-003: Random Abstraction

**Decision**: Simple `IRandomProvider` interface with a single `Next(int minValue, int maxValue)` method. Production implementation wraps `System.Random`. Test implementation returns pre-configured values for deterministic assertions.

**Rationale**: Both `HandleReportCommand` (self-report 5%, critical hit 1%) and `HandleAppeal` (50% win/loss) depend on random numbers. The current code has a bug where `HandleAppeal` creates `new Random()` instead of using the static `_random` field. Abstracting randomness fixes this bug while enabling deterministic tests.

**Alternatives considered**:
- **`Func<int, int, int>` parameter**: Simpler but less readable at call sites; interface is more idiomatic C#
- **Seed-based testing**: Pass known seed to `Random` — fragile, breaks if call order changes
- **No abstraction (test probabilistically)**: Run 10,000 iterations and check statistical distribution — slow, flaky, hard to test edge cases

## R-004: Test Database Strategy

**Decision**: In-memory SQLite via `Microsoft.EntityFrameworkCore.Sqlite` with `Data Source=:memory:` connection string. Each test creates its own connection and context. Use `dbContext.Database.EnsureCreated()` for test setup (not `MigrateAsync`) since tests don't need migration history.

**Rationale**: In-memory SQLite provides fast, isolated test databases with zero file system dependencies. Each test gets a fresh database, preventing cross-test contamination. Connection must be kept open for the lifetime of the test (SQLite in-memory DBs are destroyed when the connection closes).

**Important**: Tests use `EnsureCreated()` (creates schema without migration history) while production uses `MigrateAsync()`. This is fine because tests validate schema correctness, not migration ordering. Database initialization tests (P3) separately validate migration behavior using file-based temp SQLite databases.

**Alternatives considered**:
- **File-based SQLite per test**: Slower (disk I/O), requires cleanup, but matches production more closely
- **EF Core InMemory provider**: Doesn't support SQLite-specific behaviors (unique constraints, foreign keys), leading to false positives
- **Shared database with transactions**: Faster but risks test coupling through shared state

## R-005: Test Framework and Project Structure

**Decision**: Single `Reported.Tests` project using xUnit (latest v2.x for .NET 9.0 compatibility). Tests organized by domain: `Persistence/`, `Services/`, with a shared `Helpers/` folder for the test DbContext factory.

**Rationale**: xUnit is the .NET ecosystem standard, recommended by the original issue #30. A single test project is simpler than multiple projects (e.g., `Reported.Persistence.Tests` + `Reported.Tests`) and sufficient for the current codebase size. The project references both `Reported` and `Reported.Persistence`.

**Alternatives considered**:
- **NUnit**: Viable but xUnit is more idiomatic in modern .NET and was specified in issue #30
- **MSTest**: Microsoft's framework; less community adoption for OSS projects
- **Multiple test projects**: Unnecessary overhead for a ~1000 LOC codebase with 2 production projects

## R-006: DbContext Testability

**Decision**: Add a constructor overload to `ReportedDbContext` that accepts `DbContextOptions<ReportedDbContext>`, enabling test code to pass in-memory SQLite configuration. The existing parameterless constructor (used by production code) remains unchanged.

**Rationale**: The current `ReportedDbContext` hard-codes its SQLite connection string in `OnConfiguring`. EF Core's standard testability pattern is constructor injection of `DbContextOptions`. Adding an overload preserves backward compatibility while enabling tests to pass `UseSqlite("Data Source=:memory:")` options.

**Alternatives considered**:
- **Environment variable override in tests**: Fragile, requires test setup to set `DATABASE_PATH`, still creates file-based DB
- **Subclass for testing**: `TestReportedDbContext` overrides `OnConfiguring` — introduces a test-specific type that might diverge from production
- **Factory method / interface**: Over-engineered for the current use case

## R-007: CI Integration

**Decision**: Add a `dotnet test` step to the existing `deploy.yml` workflow, running before the Docker build. Tests must pass before any image is built or deployed.

**Rationale**: The existing pipeline already builds the .NET solution. Adding `dotnet test` as a prerequisite step is minimal effort and ensures no broken code reaches production. A separate test workflow was considered but rejected to avoid duplicating the build matrix.

**Alternatives considered**:
- **Separate test workflow file**: More flexible (can run on PRs without deploy) but duplicates setup. Could be added later.
- **Test in Dockerfile**: Runs tests inside Docker build — slower, harder to get test results out, and fails the entire build opaquely
- **GitHub Actions test reporter**: Nice for PR annotations but adds complexity; simple pass/fail is sufficient for now

## R-008: CSharpFunctionalExtensions Usage Patterns

**Decision**: Service methods return `Result<T>` for operations that can fail and `Result` (non-generic) for void-like operations. Use `Maybe<T>` for optional lookups. Use `Result.Success()` / `Result.Failure()` factory methods. Avoid deep railway chains initially — keep methods readable with explicit Result construction.

**Rationale**: The library's `Result<T>` makes success/failure explicit at the type level. For this codebase, most operations are straightforward (create report, process appeal), so deep `Bind`/`Map` chains would add complexity without benefit. Start with explicit `Result.Success(value)` / `Result.Failure<T>("error")` returns and adopt railway composition only where it simplifies multi-step flows.

**Key patterns for this project**:
- `ReportingService.CreateReport(...)` → `Result<ReportOutcome>` (success with details, or failure reason)
- `AppealService.ProcessAppeal(...)` → `Result<AppealOutcome>` (win/loss details, or failure)
- `ReportingService.GetReportsByUser(...)` → `Result<IReadOnlyList<ReportGroup>>` (query results)
- Tests assert on `result.IsSuccess`, `result.Value`, `result.Error`
