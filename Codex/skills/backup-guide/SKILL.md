---
name: backup-guide
description: DB and code backup guardrails for Wildlife Survival Server. Use before risky database operations, EF Core migration application or rollback, data cleanup, destructive commands, deployment-affecting changes, broad refactors, dependency/runtime configuration changes, or when the user asks to back up code or PostgreSQL data.
---

# backup-guide Skill

Use this skill to preserve recoverability before risky work. Prefer explicit, reviewable backups over implicit assumptions.

## Core Rule

Do not perform destructive, irreversible, production-impacting, or broad changes until the backup need is assessed and the developer approves the selected backup direction.

## Backup Decision Gate

For important backup decisions, present at least 3 options and ask the developer to choose.

Example:

```text
백업 방향은 3가지가 있습니다.
1. Git safety branch only: 빠르고 코드 변경 보호에 충분하지만 DB 데이터는 보호하지 않습니다.
2. PostgreSQL dump + Git branch: 코드와 DB를 함께 보호합니다. 시간이 조금 더 걸립니다. (추천)
3. Full manual backup outside repo: 운영/공유 DB에 적합하지만 외부 경로와 권한 확인이 필요합니다.
어느 방향으로 진행할까요?
```

## When Backup Is Required

- Before `dotnet ef database update`, rollback, migration removal, or manual migration SQL.
- Before deleting files, tables, columns, indexes, constraints, seed data, or user data.
- Before broad refactors, dependency upgrades, runtime configuration changes, or deployment changes.
- Before modifying auth/session/security behavior with existing users.
- Before force-like Git operations, branch rewrites, or conflict-heavy merges.
- Before working against a shared, staging, or production database.

## Code Backup Rules

- Prefer Git-native backups:
  - Commit completed work with `Codex/skills/save-changes/SKILL.md`.
  - For risky work, create a safety branch before edits: `backup/<scope>-yyyyMMdd-HHmm`.
  - For unfinished local work, create a patch file only after confirming its destination and excluding secrets.
- Do not copy the repository into ad hoc folders inside the repo.
- Do not include `bin/`, `obj/`, IDE metadata, caches, secrets, `.env`, `appsettings.Development.json`, or `*.user-secrets` in backup artifacts.
- Always run `git status --short` before and after backup-related work.
- Never overwrite or delete a developer's existing backup without explicit approval.

## DB Backup Rules

- Never store DB dumps inside the Git repository.
- Never commit DB dumps, real user data, credentials, connection strings, or environment-specific values.
- Prefer PostgreSQL custom-format dumps for recoverability:

```bash
pg_dump --format=custom --file <safe-external-path>.dump <connection>
```

- For schema-only review, prefer migration SQL scripts:

```bash
dotnet ef migrations script --project Wildlife-Sports-Day-Server
```

- For production/shared databases:
  - Ask for explicit confirmation of environment, database name, host, and backup destination.
  - Prefer a verified dump before applying migrations.
  - Confirm restore strategy or rollback script before applying destructive changes.

## Restore Awareness

Before risky DB work, state how rollback would work:
- Code rollback: commit hash, branch, or patch.
- Schema rollback: EF migration rollback or reviewed SQL script.
- Data rollback: PostgreSQL dump restore path.

If restore has not been verified and the operation can lose data, stop and ask.

## Stop Conditions

Stop and ask before continuing when:

- The target DB environment is unclear.
- The backup destination is inside the repository.
- Credentials would need to be written to a file or command history.
- A dump may contain real user data and no approved secure location is known.
- The requested operation can lose data but no backup/rollback direction has been selected.
