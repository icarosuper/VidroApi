# CLAUDE.md

File give guidance to Claude Code (claude.ai/code) when work with code in repo.

## Language

All code must be in English: class names, method names, variables, test names, log messages, comments, and XML docs. Only exception is commit messages, written in Portuguese.

## Git commits

**NEVER commit code without explicit user request.** Always:
1. Implement changes
2. Run tests, verify pass
3. Show user changes, suggest commit message
4. Wait for user approve or request commit

## Working style

After each implementation step:
1. **Run all tests** — `dotnet test` after finish feature. Fix failures before proceed.
2. **Update relevant docs** — reflect schema, endpoint, or design changes in `docs/plans/` and `docs/claude/features-index.md`.
3. **Suggest commit message in Portuguese** — user review and commit manually. Never commit without ask.
4. **Show next possible steps** — brief list so user choose what implement next.

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

Each feature is self-contained file under `src/VidroApi.Api/Features/<Domain>/FeatureName.cs`.

→ Read `docs/claude/architecture.md` when create new feature, endpoint, or add auth/VideoProcessor integration.

## Conventions

→ Read `docs/claude/conventions.md` before create or edit domain entities, write features, or add tests.

## Design decisions

→ Read `docs/claude/design-decisions.md` when implement deletion, counters, pagination, cascades, or MinIO cleanup.

## Features index

→ Read `docs/claude/features-index.md` to locate existing feature file before search codebase. Update whenever feature added or removed.

## Branching and release strategy

- **Feature branches** — one branch per feature group, branch off `master`, merge back via PR.
- **`master`** — always deployable. CI/CD deploy `master` HEAD to staging automatically.
- **Releases** — marked with git tag (`v1.0.0`, `v1.1.0`, etc.) on `master`. Production deploy from tags.
- **Coordination with VidroProcessor** — when change affect shared contract (MinIO paths, Redis queue name, webhook format), both repos must be tagged and deployed together.

## Implementation plan

See `docs/plans/2026-03-26-implementation-plan.md` for full task-by-task plan.