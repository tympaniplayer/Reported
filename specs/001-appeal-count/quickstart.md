# Quickstart: Appeal Count

**Feature**: 001-appeal-count
**Date**: 2026-02-10

## Prerequisites

- .NET 9.0 SDK installed
- EF Core tools installed (`dotnet tool install --global dotnet-ef`)
- `DISCORD_TOKEN` environment variable set
- Bot added to a test Discord server

## Build & Run

```bash
# From repository root
dotnet build

# Apply the new migration
dotnet ef database update --project Reported.Persistence

# Run the bot
dotnet run --project Reported
```

## Verify the Feature

### 1. Check command registration

After the bot starts, type `/` in any channel where the bot is
present. You should see `appeal-count` in the autocomplete list
alongside the existing commands.

### 2. Test with zero history

Run `/appeal-count`. You should see a message indicating zero
appeal attempts (e.g., "you haven't even tried to appeal yet").

### 3. Create some appeal history

Run `/appeal` a few times. Each real appeal (win or loss) should
be tracked. The no-reports penalty (if you have zero reports)
should NOT count as an attempt.

### 4. Verify counts

Run `/appeal-count` again. The message should show your correct
win count, attempt count, and win rate percentage.

### 5. Verify persistence

Stop and restart the bot. Run `/appeal-count` again. The counts
should be the same as before the restart.

## Validation Checklist

- [ ] `/appeal-count` appears in Discord slash command autocomplete
- [ ] Zero-history users see an appropriate message
- [ ] Winning an appeal increments both wins and attempts
- [ ] Losing an appeal increments only attempts
- [ ] No-reports penalty does NOT increment either counter
- [ ] Counts persist across bot restarts
- [ ] Response is public (visible to everyone in the channel)
- [ ] Response tone matches the bot's playful personality
- [ ] Serilog logs show appeal-count query timing
