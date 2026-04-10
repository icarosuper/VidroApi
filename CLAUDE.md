# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Language

All code must be in English: class names, method names, variables, test names, log messages, comments, and XML docs. The only exception is commit messages, which are written in Portuguese.

## Git commits

**NEVER commit code without explicit user request.** Always:
1. Implement the changes
2. Run tests to verify they pass
3. Show the user the changes and suggest a commit message
4. Wait for the user to approve or request the commit

## Working style

After each implementation step:
1. **Run all tests** — `dotnet test` after finishing a feature. Fix any failures before proceeding.
2. **Update relevant docs** — reflect any schema, endpoint, or design changes in `docs/plans/` and `docs/claude/features-index.md`.
3. **Suggest a commit message in Portuguese** — the user reviews and commits manually. Never commit without being asked.
4. **Show the next possible steps** — brief list so the user can choose what to implement next.

## Commands

```bash
# Build
dotnet build

# Run API (development)
dotnet run --project src/VidroApi.Api

# All tests
dotnet test

# Single test class
dotnet test tests/VidroApi.UnitTests --filter "FullyQualifiedName~ClassName"

# Single test method
dotnet test tests/VidroApi.UnitTests --filter "FullyQualifiedName~ClassName.MethodName"

# EF Core migrations (always specify both projects)
# Migration names must follow the pattern: PascalCaseDescriptionMigration (e.g. AddCommentsFeatureMigration, ChangeUsernameMaxLengthMigration)
dotnet ef migrations add <DescriptionMigration> --project src/VidroApi.Infrastructure --startup-project src/VidroApi.Api --output-dir Persistence/Migrations
dotnet ef database update --project src/VidroApi.Infrastructure --startup-project src/VidroApi.Api

# Start dependencies
docker-compose up -d postgres redis minio
```

## Architecture overview

```
Domain ← Application ← Infrastructure ← Api
```

Each feature is a self-contained file under `src/VidroApi.Api/Features/<Domain>/FeatureName.cs`.

→ Read `docs/claude/architecture.md` when creating a new feature, endpoint, or adding auth/VideoProcessor integration.

## Conventions

→ Read `docs/claude/conventions.md` before creating or editing domain entities, writing features, or adding tests.

## Design decisions

→ Read `docs/claude/design-decisions.md` when implementing deletion, counters, pagination, cascades, or MinIO cleanup.

## Features index

→ Read `docs/claude/features-index.md` to locate an existing feature file before searching the codebase. Update it whenever a feature is added or removed.

## Branching and release strategy

- **Feature branches** — one branch per feature group, branching off `master` and merged back via PR.
- **`master`** — always deployable. CI/CD deploys `master` HEAD to staging automatically.
- **Releases** — marked with a git tag (`v1.0.0`, `v1.1.0`, etc.) on `master`. Production deploys from tags.
- **Coordination with VidroProcessor** — when a change affects the shared contract (MinIO paths, Redis queue name, webhook format), both repos must be tagged and deployed together.

## Implementation plan

See `docs/plans/2026-03-26-implementation-plan.md` for the full task-by-task plan.
