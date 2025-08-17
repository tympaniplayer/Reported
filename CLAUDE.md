# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a Discord bot called "Reported" that allows users to playfully "report" each other with various silly reasons. It's a meme bot designed for fun among friends, inspired by the gaming community habit of jokingly saying "reported" when someone does something noteworthy.

## Architecture

The project is structured as a .NET 9.0 solution with two main projects:

- **Reported** - Main console application containing the Discord bot logic
- **Reported.Persistence** - Data access layer using Entity Framework Core with SQLite

### Core Components

- `Program.cs` - Entry point, Discord client setup, command handlers, and main bot logic
- `Commands.cs` - Discord slash command definitions 
- `Constants.cs` - Report reason aliases and descriptions
- `ReportedDbContext.cs` - Entity Framework database context
- `UserReport.cs` - Main entity representing a user report
- `AxiomHttpClient.cs` - Custom HTTP client for Axiom logging integration

### Key Features

- **Report Command** - Users can report others with predefined reasons (aliases like "NA" for "Negative Attitude")
- **Statistics Commands** - `who-reported`, `why-reported` to show report statistics
- **Appeal System** - Random chance-based appeal process that may remove or add reports
- **Critical Hits** - 1% chance for double reports
- **Self-Report Penalty** - 5% chance to backfire and report the initiator instead

## Development Commands

### Build and Run
```bash
dotnet build
dotnet run --project Reported
```

### Database Management
```bash
# Create and apply migrations
dotnet ef migrations add <MigrationName> --project Reported.Persistence
dotnet ef database update --project Reported.Persistence
```

### Publishing
```bash
dotnet publish
```

## Environment Variables

Required environment variables:
- `DISCORD_TOKEN` - Discord bot token
- `AXIOM_TOKEN` - Axiom API token for logging
- `AXIOM_DATASET` - Axiom dataset name

## Database

Uses SQLite with Entity Framework Core. Database file is stored in `LocalApplicationData/reported.db`. The bot automatically creates the database and applies pending migrations on startup.

## External Dependencies

- **Discord.Net** - Discord API wrapper
- **Serilog** - Logging with console and HTTP (Axiom) sinks
- **Entity Framework Core** - ORM with SQLite provider
- **Axiom** - Log aggregation service (can be removed by commenting out relevant code)

## Deployment

The project includes a systemd service file (`Reported.service`) for Linux deployment. Designed to run on small VPS or dedicated hardware like a Raspberry Pi.