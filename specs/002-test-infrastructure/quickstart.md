# Quickstart: Comprehensive Test Infrastructure

**Branch**: `002-test-infrastructure` | **Date**: 2026-02-15

## Prerequisites

- .NET 9.0 SDK installed
- Git with `002-test-infrastructure` branch checked out

## Running Tests

```bash
# Run all tests from solution root
dotnet test

# Run with verbose output
dotnet test --verbosity normal

# Run a specific test class
dotnet test --filter "FullyQualifiedName~ReportingServiceTests"
```

## Project Structure After Implementation

```
Reported.sln
├── Reported/                          # Main bot project (existing)
│   ├── Program.cs                     # Thinner — delegates to services
│   ├── Commands.cs                    # Unchanged
│   ├── Constants.cs                   # Unchanged
│   ├── Services/                      # NEW — extracted business logic
│   │   ├── IRandomProvider.cs         # Random abstraction interface
│   │   ├── RandomProvider.cs          # Production Random wrapper
│   │   ├── ReportingService.cs        # Report creation + statistics
│   │   └── AppealService.cs           # Appeal processing + stats
│   ├── Models/                        # NEW — Result value types
│   │   ├── ReportOutcome.cs           # Report operation result
│   │   ├── AppealOutcome.cs           # Appeal operation result
│   │   ├── ReportGroup.cs             # Statistics aggregation
│   │   └── AppealStats.cs             # Appeal statistics
│   └── External/
│       └── AxiomHttpClient.cs         # Unchanged
│
├── Reported.Persistence/              # Data layer (existing, minor change)
│   ├── ReportedDbContext.cs           # Added: DbContextOptions constructor overload
│   ├── UserReport.cs                  # Unchanged
│   ├── AppealRecord.cs               # Unchanged
│   └── ...
│
└── Reported.Tests/                    # NEW — test project
    ├── Reported.Tests.csproj          # xUnit + references to both projects
    ├── Helpers/
    │   ├── TestDbContextFactory.cs    # Creates in-memory SQLite contexts
    │   └── FakeRandomProvider.cs      # Deterministic IRandomProvider for tests
    ├── Persistence/
    │   ├── UserReportTests.cs         # Report CRUD operations
    │   ├── AppealRecordTests.cs       # Appeal upsert + unique constraint
    │   └── DatabaseInitTests.cs       # Migration + initialization tests
    └── Services/
        ├── ReportingServiceTests.cs   # Report mechanics (crits, self-reports)
        ├── AppealServiceTests.cs      # Appeal logic (win/loss, no-reports penalty)
        └── StatisticsTests.cs         # who-reported, why-reported queries
```

## Key Implementation Notes

### DbContext in Tests

Tests create isolated in-memory SQLite databases. The connection must stay open for the test's lifetime:

```csharp
// Pattern: create connection → open → create options → create context → EnsureCreated
var connection = new SqliteConnection("Data Source=:memory:");
connection.Open();
var options = new DbContextOptionsBuilder<ReportedDbContext>()
    .UseSqlite(connection)
    .Options;
using var context = new ReportedDbContext(options);
context.Database.EnsureCreated();
```

### Deterministic Random in Tests

Tests use a `FakeRandomProvider` that returns predetermined values:

```csharp
// Force self-report backfire (needs value < 5)
var fakeRandom = new FakeRandomProvider(returnValues: [3]);
var service = new ReportingService(context, fakeRandom);

// Force critical hit (needs value == 1)
var fakeRandom = new FakeRandomProvider(returnValues: [50, 1]); // first: no self-report, second: critical
```

### CSharpFunctionalExtensions in Tests

```csharp
var result = await service.CreateReport(...);
Assert.True(result.IsSuccess);
Assert.Equal(1, result.Value.ReportCount);
Assert.False(result.Value.IsCriticalHit);
```

## Adding New Tests

1. Create test class in appropriate subdirectory (`Persistence/` or `Services/`)
2. Use `[Fact]` for single-case tests, `[Theory]` with `[InlineData]` for parameterized tests
3. Create a fresh `TestDbContextFactory.Create()` in each test for isolation
4. Assert on `Result.IsSuccess` / `Result.IsFailure` and inspect `.Value` / `.Error`
