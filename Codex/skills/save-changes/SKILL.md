---
name: save-changes
description: Safe Git commit and PR workflow for Wildlife Survival Server. Use when the user asks to save, commit, checkpoint, preserve, push, publish, open a PR, prepare a PR description, or review current changes by inspecting the worktree, separating unrelated edits, validating relevant changes, and following project commit/PR rules.
---

# Save Changes

## Overview

Use this skill to turn local worktree changes into an intentional Git commit and, when requested, a safe pull request. Preserve user changes, keep commits scoped by staged content, and follow this repository's Korean commit convention.

## Workflow

1. Inspect the worktree with `git status --short`.
2. Review changed content before staging:
   - Use `git diff -- <path>` for modified tracked files.
   - Use `Get-Content` or targeted file reads for new files.
   - Use `git diff --cached` if anything is already staged.
3. Create a per-file change summary before staging. For every changed file, record:
   - `path`
   - status: added / modified / deleted / renamed
   - purpose: why it changed
   - action: stage / leave unstaged / ask user
   - verification needed: build / test / syntax check / docs-only
4. Update the per-file summary whenever another file changes before committing. Do not wait until the final response to sort the file list.
5. Separate changes into commit units by domain or purpose.
6. Do not stage unrelated files, generated caches, IDE metadata, local secrets, or changes the current agent did not make unless the user explicitly asks.
7. If unrelated existing changes are present, mention them and leave them unstaged.
8. Run the narrowest useful verification before committing. Prefer `dotnet build` for backend code changes; skip only when the change is documentation or hook-only and explain that choice.
9. Stage only the selected files with explicit paths.
10. Recheck `git diff --cached` and `git status --short`.
11. Commit with the repository convention.
12. Report the commit hash, message, verification result, per-file summary, and any remaining unstaged changes.
13. Do not push or open a PR unless the user explicitly asks.

## Ambiguous Commit Requests

When the user asks to modify, fix, rewrite, amend, reword, reorder, squash, or otherwise change commits, first identify the exact target commit or commit range before running any history-changing command.

- If the user names a commit hash, branch range, or clear target such as "the last commit", operate only on that target.
- If the request can refer to pushed commits, unpushed commits, multiple commits, or the whole branch, stop and ask which commit or range should be changed.
- Do not infer that every commit in the branch should be modified from a general request such as "커밋을 수정해줘", "본문을 추가해줘", or "이슈 번호를 넣어줘".
- Before changing pushed commits, confirm whether rewriting published history is acceptable and preserve a safety branch.
- Prefer non-history-changing fixes, such as a follow-up commit, unless the user explicitly asks to rewrite existing commits.

## Pre-Commit Backup Check

Before committing risky work, load `Codex/skills/backup-guide/SKILL.md` and confirm the backup/rollback path.

Risky work includes:
- EF Core migrations, database updates, data cleanup, or schema changes.
- Auth/session/security behavior changes.
- Broad refactors, dependency upgrades, runtime configuration changes, or file deletions.
- Any change that would be hard to revert from Git alone.

## Commit Message

Use this exact format:

```text
type: 설명

#<이슈번호>
```

Allowed types:

- `feat`
- `fix`
- `docs`
- `refactor`
- `test`
- `ci`
- `chore`

Do not include scope in the commit title. Include the domain or meaningful project area naturally in the Korean description when useful, such as auth, score, ranking, email, or Codex instructions.

Description must be Korean and must not end with a period.

If the branch already has a pull request, put the PR number on the first body line as `#<PR번호>` with no space, for example `#123`. If no pull request exists but a related issue exists, put the issue reference on the first body line as `#<이슈번호>`. If the PR or issue number is unknown, find it from GitHub context or ask the user before committing. Do not omit the body issue line when a PR or related issue exists.

Examples:

```text
feat: 점수 저장 검증 추가

#123

fix: 이메일 인증 예외 처리 개선
docs: 변경사항 저장 스킬 갱신
```

## Commit Rules

- One commit should represent one logical change.
- Do not mix feature, refactor, formatting, migration, and test-only changes unless they are inseparable.
- Split commits by functional unit even when all changes are under `Codex/`.
- For Codex resource changes, prefer separate commits for each meaningful area:
  - One commit per skill type or skill folder, such as `backup-guide`, `migration-guide`, or `save-changes`.
  - One commit for root/shared instructions such as `AGENTS.md` and `Codex/CODEX.md` when they are changed together for the same policy.
  - One commit for hook changes under `Codex/hooks/`.
  - One commit for subagent prompt changes under `Codex/agents/`.
  - One commit for settings/config changes only when they are not inseparable from the hook or skill change.
- If a single request changes multiple independent domains, create multiple commits instead of one broad `chore: Codex 규칙 갱신` commit.
- Include EF Core migration files in the same commit as the model/configuration change that requires them.
- Do not commit generated build output, local IDE state, DB dumps, secrets, or unrelated user changes.
- If verification fails, stop before committing unless the user explicitly asks to commit with a known failure.
- For Codex instruction, skill, hook, or agent changes, use `docs: ...` for documentation-only changes or `chore: ...` when the change adjusts Codex workflow behavior.

## PR Rules

Open or update a PR only when the user explicitly asks to push, publish, create PR, or prepare PR.

Before PR:
- Confirm the working tree has no unintended staged/unstaged changes for the PR scope.
- Confirm the branch name is appropriate: `feature/<scope>-<short-name>`, `fix/<scope>-<short-name>`, `docs/<scope>-<short-name>`, or `codex/<short-name>`.
- Confirm relevant verification has run and report failures honestly.
- Never force-push, retarget base branch, mark ready for review, request reviewers, or merge without explicit approval.

PR title:

```text
type: 설명
```

PR body:

```markdown
## 요약
- ...

## 검증
- `dotnet build`

## 영향 범위
- ...

## 마이그레이션 / 백업
- 마이그레이션: 있음/없음
- DB 백업 필요 여부: 필요/불필요
- 롤백 경로: ...
```

For PRs containing migrations or DB-impacting changes, include backup status and rollback path in the PR body.

## Safety Rules

- Never run `git reset --hard`, `git checkout --`, `git clean`, or force-push commands as part of this skill.
- Never commit secrets, real credentials, `appsettings.Development.json`, `.env`, `*.user-secrets`, build output, cache folders, or IDE metadata unless the user explicitly requests a repository metadata change after review.
- Never hide test or build failures. If verification fails, stop before committing unless the user explicitly asks to commit anyway.
- Ask a concise question only when the intended commit scope is ambiguous and cannot be inferred safely.
- Do not push after committing unless the user explicitly asks.
- Do not open a PR until the final branch, base branch, title, and body are clear.

## Per-File Summary Format

Use this compact format when saving changes:

```text
## 파일별 정리
- `path/to/file`: modified, stage — 변경 이유 — 검증: docs-only
- `path/to/other`: modified, leave unstaged — 사용자 기존 변경으로 판단 — 검증: 없음
```
