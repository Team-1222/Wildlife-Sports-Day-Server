---
name: migration-guide
description: EF Core migration workflow guardrails for Wildlife Survival Server. Use when Codex needs to plan, create, inspect, review, rename, remove, script, or apply database migrations; when entity or FluentAPI changes may alter PostgreSQL schema; or when deciding whether a schema/data migration is safe.
---

# migration-guide Skill

Use this skill for EF Core migration work in the ASP.NET Core + PostgreSQL backend.

## Core Rule

Treat migrations as schema-changing operations with data-loss risk. Do not create, remove, rewrite, or apply migrations unless the user asked for that migration task or explicitly approved the proposed direction.

## Decision Gate

Before any important migration decision, present at least 3 options and ask the developer to choose. Include the tradeoff and recommended option.

Important decisions include:
- Changing table or column names, types, nullability, defaults, indexes, unique constraints, foreign keys, cascade rules, or enum persistence.
- Splitting one model change across multiple migrations or combining unrelated model changes into one migration.
- Adding backfill, data cleanup, seed data, destructive operations, or manual SQL.
- Running `dotnet ef database update`, rolling back a database, or generating a production SQL script.
- Editing generated migration files by hand.

Example:

```text
선택지가 3가지 있습니다.
1. Add nullable column first, backfill later: safest for existing data, two migrations.
2. Add required column with default: simpler deployment, default must be business-correct.
3. Add required column and manual backfill SQL: precise but higher review burden.
추천: 1번, 기존 데이터 손상 위험이 가장 낮습니다.
어느 방향으로 진행할까요?
```

## Workflow

1. Inspect current entity, enum, DbContext, FluentAPI configuration, and existing `Migrations/`.
2. Confirm the intended domain behavior before schema behavior.
3. Identify schema impact: tables, columns, indexes, constraints, relationships, defaults, and data movement.
4. If the change is important, stop and provide at least 3 options for the developer to select.
5. Build before migration generation when feasible: `dotnet build`.
6. Generate migration only after model/configuration changes are complete.
7. Review generated `Up` and `Down` methods before accepting the migration.
8. Run `dotnet build` again after migration generation.
9. Do not run `dotnet ef database update` unless explicitly requested or approved.

## Commands

Run commands from the repository root unless the project layout requires otherwise.

```bash
dotnet build
dotnet ef migrations add AddSomething --project Wildlife-Sports-Day-Server
dotnet ef migrations remove --project Wildlife-Sports-Day-Server
dotnet ef migrations script --project Wildlife-Sports-Day-Server
dotnet ef database update --project Wildlife-Sports-Day-Server
```

Use PascalCase verb+noun migration names:

```text
AddEmailVerificationCode
CreateScoreTable
UpdateUserNicknameLength
```

## Review Checklist

- Migration files are included with the model/configuration change.
- Migration name describes the domain/schema change, not the implementation layer.
- `Up` and `Down` are symmetrical enough to support rollback.
- No accidental table drops, column drops, or type changes appear.
- No raw SQL is used unless reviewed and justified.
- New required columns on existing tables have a safe default or staged nullable rollout.
- Indexes support expected query patterns and do not duplicate existing indexes.
- Foreign key delete behavior is explicit when deletion can cascade.
- No secrets, connection strings, or environment-specific values are committed.

## Safe Defaults

- Prefer staged migrations for risky existing-data changes: add nullable column, deploy/backfill, then enforce non-null.
- Prefer FluentAPI for table names, column names, indexes, constraints, and relationships.
- Prefer EF Core provider-generated operations over handwritten SQL.
- Prefer generating SQL scripts for production review instead of applying migrations directly.

## Manual Edits

Edit generated migration files only when EF Core cannot express the intended safe operation clearly. When manual edits are needed:

- Explain why EF-generated output is insufficient.
- Keep manual SQL minimal and parameter-free.
- Ensure `Down` handles rollback realistically.
- Ask for developer approval before accepting destructive SQL.

## Stop Conditions

Stop and ask before continuing when:

- A migration would drop or truncate data.
- The current model and generated migration disagree in a non-obvious way.
- The database provider or startup project cannot be determined.
- `dotnet ef` cannot run because tooling, connection strings, or design-time services are missing.
- Production or shared database update is requested without an explicit confirmation.
