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

# Start dependencies (compose lives in the parent dir, alongside the three repos)
docker compose -f ../docker-compose.yml up -d postgres redis minio

# Or the whole stack — API, front, processor, observability
cd .. && docker compose up -d --build
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

- **Commits go straight to `master`** by default (small changes, bugfixes). Only a large multi-commit feature gets a `feature/<topic>` branch — and only after asking the user. See "Onde commitar".
- **`master`** — always deployable. **No deploy pipeline exists yet**: `.github/workflows/ci.yml` only builds and tests on PRs to `master`. Deploys are manual.
- **Releases** — intended strategy: git tag (`v1.0.0`, `v1.1.0`, etc.) on `master`, production deploy from tags. Not in use yet — the repo has no tags.
- **Coordination with VidroProcessor** — when change affect shared contract (MinIO paths, Redis queue name, webhook format), both repos must be tagged and deployed together.

## Implementation plan

See `docs/plans/2026-03-26-implementation-plan.md` for full task-by-task plan.

## Padrão de mensagem de commit

Conventional Commits, **em português**, só o assunto — sem corpo, sem escopo, sem rodapé (nada de `Co-authored-by`).

Formato: `<tipo>: <verbo no infinitivo> <complemento>` — minúsculo depois do tipo, sem ponto final, até ~72 chars.

Tipos usados no repo (frequência real): `feat` > `chore` > `fix` > `refactor` > `docs` / `test`.

- `feat` — funcionalidade nova ou ampliada
- `fix` — correção de bug/comportamento
- `chore` — docs, README, migrations, scaffold, reorganização sem lógica
- `refactor` — renomear/reestruturar sem mudar comportamento
- `docs` / `test` — quando a mudança é só documentação ou só teste

Exemplos do histórico: `feat: adicionar upload de avatar do canal`, `fix: corrigir botão de reações`, `chore: atualizar README`, `refactor: renomear projeto`.

Título de PR (squash merge): `Feature/nome-da-branch (#N)`.

### Onde commitar

- **Padrão: direto na `master`.** Coisa pequena e bugfix não abre branch.
- **Exceção: feature grande** (vários commits). Aí **pergunte ao usuário** se é para criar `feature/<topic>` ou mandar direto para `master` — nunca decida sozinho.
- `master` sempre deployável; produção sai de tags `vX.Y.Z` (estratégia pretendida — ainda não há tag nenhuma no repo).
