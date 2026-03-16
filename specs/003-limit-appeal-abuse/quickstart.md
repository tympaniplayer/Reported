# Quickstart: Limit Appeal Abuse

**Branch**: `003-limit-appeal-abuse` | **Date**: 2026-03-13

## What's Changing

The appeal system (`/appeal` command) is being hardened against abuse:
1. Each report can only be appealed once (win or lose, it's done)
2. Self-reports (backfire, intentional, or penalty) cannot be appealed
3. Clear rejection messages when no eligible reports exist

## Files to Modify

### Reported.Persistence (data layer)
- `UserReport.cs` — Add `HasBeenAppealed` bool property (constructor default: `false`)
- `Configuration/UserReportConfiguration.cs` — Add `HasDefaultValue(true)` for migration
- New migration via `dotnet ef migrations add AddHasBeenAppealedToUserReport --project Reported.Persistence --startup-project Reported.Persistence`

### Reported (bot logic)
- `Models/AppealOutcome.cs` — Add `RejectionReason` field
- New file: `Models/AppealRejectionReason.cs` — Enum: `None`, `AllAppealed`, `OnlySelfReports`
- `Services/AppealService.cs` — Update `ProcessAppeal` to filter eligible reports and set `HasBeenAppealed`
- `Program.cs` — Update `HandleAppeal` to display rejection-specific messages

### Reported.Tests (tests)
- `Services/AppealServiceTests.cs` — Update existing tests, add new tests for eligibility filtering and rejection reasons

## Build & Test

```bash
# Build
dotnet build

# Run tests
dotnet test

# Create migration (after entity changes)
dotnet ef migrations add AddHasBeenAppealedToUserReport --project Reported.Persistence --startup-project Reported.Persistence
```
