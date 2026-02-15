<!--
  ══════════════════════════════════════════════════════════════
  Sync Impact Report
  ══════════════════════════════════════════════════════════════
  Version change: (none) → 1.0.0 (initial ratification)
  Modified principles: N/A (initial creation)
  Added sections:
    - Core Principles (5 principles)
    - Technology & Deployment Constraints
    - Development Workflow
    - Governance
  Removed sections: N/A
  Templates requiring updates:
    - .specify/templates/plan-template.md        ✅ no changes needed
    - .specify/templates/spec-template.md         ✅ no changes needed
    - .specify/templates/tasks-template.md        ✅ no changes needed
    - .specify/templates/checklist-template.md    ✅ no changes needed
    - .specify/templates/commands/               ✅ directory empty, no updates
  Follow-up TODOs: None
  ══════════════════════════════════════════════════════════════
-->

# Reported Constitution

## Core Principles

### I. Simplicity & Fun First

Every feature MUST serve the bot's core purpose: lighthearted,
meme-driven social interaction in Discord. Complexity is only
justified when it directly enhances the player experience.

- New features MUST be explainable in one sentence to a
  non-technical Discord user.
- YAGNI (You Aren't Gonna Need It) applies strictly — do not
  build abstractions, configuration layers, or extension points
  for hypothetical future needs.
- If a feature requires more than two new source files, it MUST
  be justified in the spec or plan with a clear rationale.

### II. Separation of Concerns

The solution MUST maintain a clear boundary between bot logic
and data persistence.

- The `Reported` project owns Discord interactions, command
  handling, and business logic (randomness, critical hits,
  appeals).
- The `Reported.Persistence` project owns all database access,
  entity definitions, and migrations.
- No Discord-specific types (e.g., `SocketSlashCommand`) may
  appear in the persistence layer.
- New cross-cutting concerns (e.g., caching, rate limiting)
  MUST be placed in the appropriate layer, not smeared across
  both.

### III. Discord API Compliance

All bot interactions MUST comply with Discord's API contracts
and rate limits.

- Each slash command handler MUST call `RespondAsync` or
  `DeferAsync` exactly once before any follow-up messages.
- Embeds, messages, and component interactions MUST respect
  Discord's documented size limits.
- New commands MUST be registered through the existing
  `Commands.cs` pattern using Discord.Net's slash command
  builder.

### IV. Observability

The bot MUST produce structured, queryable logs for every
meaningful operation.

- All command executions, errors, and significant state changes
  MUST be logged via Serilog.
- Log messages MUST use structured properties (not string
  interpolation) so they are machine-parseable.
- Axiom integration is optional at runtime (graceful degradation
  when tokens are absent), but the logging code path MUST always
  execute against at least the console sink.

### V. Data Integrity

User report data MUST be treated as the bot's most valuable
asset.

- All schema changes MUST go through Entity Framework Core
  migrations — no manual SQL or ad-hoc schema edits.
- Database initialization on startup MUST be idempotent and
  safe to run repeatedly.
- Destructive data operations (bulk deletes, schema drops) MUST
  NOT be triggered by regular user commands.

## Technology & Deployment Constraints

- **Runtime**: .NET 9.0 (C#, nullable reference types enabled).
- **Discord Library**: Discord.Net (current: 3.17.x).
- **ORM**: Entity Framework Core with SQLite provider.
- **Logging**: Serilog with Console and HTTP (Axiom) sinks.
- **Deployment**: Docker container or systemd service on Linux.
  The bot MUST run reliably on low-resource hardware (e.g.,
  Raspberry Pi, small VPS).
- **Secrets**: `DISCORD_TOKEN`, `AXIOM_TOKEN`, and
  `AXIOM_DATASET` are provided via environment variables. Secrets
  MUST NOT be committed to the repository.

## Development Workflow

- **Build**: `dotnet build` MUST succeed with zero warnings
  treated as errors before any PR is merged.
- **Migrations**: Created via
  `dotnet ef migrations add <Name> --project Reported.Persistence`.
- **Publishing**: `dotnet publish` for release artifacts.
- **Branching**: Feature branches off `main`; PRs target `main`.
- **Commit messages**: Conventional-style prefixes encouraged
  (e.g., `feat:`, `fix:`, `docs:`).
- **Code style**: Follow existing patterns in the codebase.
  Consistency with neighboring code takes precedence over
  external style guides.

## Governance

This constitution is the authoritative source of project
principles and constraints. All specifications, plans, and task
lists produced by SpecKit workflows MUST be consistent with
these principles.

- **Amendments**: Any change to this constitution MUST be
  documented with a version bump, rationale, and updated
  `LAST_AMENDED_DATE`.
- **Versioning**: Constitution versions follow semantic
  versioning — MAJOR for principle removals or incompatible
  redefinitions, MINOR for new principles or material
  expansions, PATCH for clarifications and wording fixes.
- **Compliance**: Feature specs and plans SHOULD include a
  Constitution Check section that validates alignment with
  these principles before implementation begins.
- **Guidance file**: `CLAUDE.md` at the repository root serves
  as the runtime development guidance file and MUST remain
  consistent with this constitution.

**Version**: 1.0.0 | **Ratified**: 2026-02-10 | **Last Amended**: 2026-02-10
