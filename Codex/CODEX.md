# Wildlife Survival Server

**한국어로 응답하고 작업해주세요. (Please respond and work in Korean)**

## Project Overview

Backend server for the Wildlife Survival game.
ASP.NET Core REST API providing login, registration (with email verification), score saving, and ranking features.

## Tech Stack

- **Backend**: C#, ASP.NET Core
- **Database**: PostgreSQL (main data), ASP.NET Core Session Middleware
- **ORM**: Entity Framework Core (EF Core)
- **Email**: MailKit (Gmail SMTP)
- **Security**: BCrypt password hashing, environment variables / User Secrets for sensitive data

## Project Structure

```
WildlifeSurvivalServer/
├── Controllers/         # API endpoints (AuthController, ScoreController, RankingController)
├── Services/            # Business logic (IAuthService, IScoreService, IRankingService)
├── Repositories/        # EF Core DB access (IUserRepository, IScoreRepository)
├── Models/              # EF Core Entities
│   └── Enums/           # State enumerations
├── Dtos/                # Request / Response DTOs
│   ├── Requests/
│   └── Responses/
├── Middleware/          # Exception handling middleware
├── Exceptions/          # Custom exceptions
└── Infrastructure/      # EF Core DbContext, email sender, etc.
```

Unidirectional dependency: `Controller → Service → Repository`

## Codex Resource Structure

```
/.codex/
└── hooks.json           # Project-local Codex hook configuration

Codex/
├── agents/              # Project-specific subagent prompts
│   ├── implementer.md
│   ├── inform-finder.md
│   ├── git-manager.md
│   ├── planner.md
│   ├── reviewer.md
│   ├── security-guardian.md
│   └── tester.md
├── hooks/               # Node hook scripts referenced by .codex/hooks.json
├── skills/              # Project-specific skill folders
│   ├── aspnet-arch/SKILL.md
│   ├── auth-security/SKILL.md
│   ├── backup-guide/SKILL.md
│   ├── code-review/SKILL.md
│   ├── efcore-guide/SKILL.md
│   ├── migration-guide/SKILL.md
│   ├── save-changes/SKILL.md
│   └── security-checklist/SKILL.md
└── CODEX.md
```

## Commands

- Build: `dotnet build`
- Run: `dotnet run`
- Add migration: `dotnet ef migrations add <Name>`
- Apply DB: `dotnet ef database update`
- Init User Secrets: `dotnet user-secrets init`
- Set User Secret: `dotnet user-secrets set "Key" "Value"`

## Coding Rules

- Follow SOLID principles — especially SRP and DIP
- Interface + implementation pair required: `IXxxService` / `XxxService`
- Constructor injection only — no `[FromServices]` field injection
- All async methods must have `Async` suffix
- Null checks use `is null` / `is not null` pattern
- No unnecessary comments — only where logic is not self-evident

### Naming Conventions

| Target | Rule | Example |
|--------|------|---------|
| Class / Interface | PascalCase | `AuthService`, `IUserRepository` |
| Method | PascalCase | `RegisterAsync`, `SendVerificationEmailAsync` |
| Variable / Parameter | camelCase | `userId`, `emailCode` |
| Constant | UPPER_SNAKE_CASE | `MAX_RANK_COUNT` |
| DTO (request) | `XxxRequest` | `RegisterRequest`, `LoginRequest` |
| DTO (response) | `XxxResponse` | `LoginResponse`, `RankingResponse` |
| Enum member | PascalCase | `RegisterEmailCodeStatus.Verified` |
| DB column | snake_case (mapped via EF Core FluentAPI) | `created_at`, `user_id` |

### API Response Format

All APIs respond with `ApiResponse<T>` wrapper:

```csharp
// Success
{ "success": true, "data": { ... } }

// Failure
{ "success": false, "error": { "code": "USER_NOT_FOUND", "message": "사용자를 찾을 수 없습니다." } }
```

### Exception Handling

- Use `AppException` directly — no subclassing
- Message: Korean (합쇼체) + period, no dynamic data (IDs, names)
- Correct: `new AppException("사용자를 찾을 수 없습니다.", StatusCodes.Status404NotFound)`
- Wrong: `new AppException($"사용자 ID: {id} 없음", 404)`

### Logging

- English verb-led sentences
- Use `{}` placeholder (ILogger structured logging)
- Correct: `_logger.LogInformation("Deleted {Count} expired email codes", count)`
- Wrong: `_logger.LogError($"에러: {message}")`

### Commit Convention

Title format: `type: 설명`

- Body first line: add `#<issue-number>` when a related issue exists, for example `#123`
- If the issue number is unknown, ask before committing; if there is no related issue, omit the issue line
- Types: `feat` / `fix` / `docs` / `refactor` / `test` / `ci` / `chore`
- Do not include scope in the commit title; include the domain naturally in the Korean description when useful
- Description: Korean, no period
- Example:

```text
feat: 이메일 인증 코드 발송 기능 추가

#123
```

### Commit / PR Rules

- Use `Codex/skills/save-changes/SKILL.md` before committing, pushing, publishing, or opening a PR.
- One commit must contain one logical change; do not mix unrelated feature, refactor, formatting, migration, or test-only changes.
- Split commits by functional unit. For Codex resource changes, separate skill folders, shared instructions (`AGENTS.md` / `Codex/CODEX.md`), hooks, subagent prompts, and settings unless they are inseparable.
- Use `Codex/agents/git-manager.md` for commit splitting, staged-scope checks, and PR preparation.
- When multiple skills are changed, prefer one commit per skill type/folder.
- Include EF Core migration files in the same commit as the model/configuration change that requires them.
- Never commit secrets, DB dumps, real user data, generated build output, IDE metadata, or unrelated user changes.
- Do not push, force-push, open PRs, request reviewers, mark ready for review, retarget base branches, or merge unless explicitly requested or approved.
- PR title follows `type: 설명`.
- PR body must include Summary, Verification, Impact Scope, and Migration/Backup sections.
- PRs with DB-impacting changes must document backup status and rollback path.

## Security Requirements

- DB password, Gmail SMTP credentials → environment variables or User Secrets only
- Sensitive values in `.env`, `appsettings.Development.json` → must be in `.gitignore`
- Passwords: BCrypt hashing required (`BCrypt.Net-Next`)
- Email verification code: 6-digit random, 5-minute expiry, single-use
- SQL Injection: use EF Core parameter binding (no Raw SQL)
- Session cookie: `HttpOnly`, `Secure`, `SameSite=Strict`

## Autonomy Guardrails

- Do not make broad, speculative, or "nice to have" changes outside the user's requested scope.
- Do not refactor unrelated code while implementing a feature or fix unless the user explicitly approves it.
- Do not change public API contracts, database schema, authentication/session behavior, security policy, dependency versions, or deployment/runtime configuration by assumption.
- For important, irreversible, security-sensitive, data-affecting, or architecture-affecting decisions, present at least 3 viable directions with tradeoffs and ask the developer to choose before implementing.
- If one option is recommended, label it as recommended and explain why, but still wait for the developer's selection.
- Small local implementation details may be chosen directly only when they do not alter behavior, schema, security, public contracts, or project conventions.
- If requirements are ambiguous and multiple valid implementations exist, stop and ask instead of silently choosing a broad direction.

## Backup Rules

- Use `Codex/skills/backup-guide/SKILL.md` before risky database, migration, deletion, broad refactor, dependency/runtime, deployment, or production/shared DB work.
- Assess whether code backup, DB backup, or both are required before risky work.
- For important backup decisions, present at least 3 options with tradeoffs and wait for developer selection.
- Prefer Git branches/commits for code backup; do not create ad hoc repository copies inside the repo.
- Never store DB dumps, real user data, credentials, or backup archives inside the repository.
- Confirm rollback path before applying migrations, destructive SQL, data cleanup, or deployment-impacting changes.

## Common Mistakes

- **Wrong**: hardcoded DB password in `appsettings.json` → **Correct**: User Secrets / env vars
- **Wrong**: business logic in `Repository` → **Correct**: handle in `Service` layer
- **Wrong**: `Controller` directly injects `DbContext` → **Correct**: go through `Service`
- **Wrong**: `async void` method → **Correct**: `async Task`
- **Wrong**: scoped commit title `fix(service): 로그인 오류 수정` → **Correct**: unscoped title `fix: 로그인 오류 수정`
- **Wrong**: silently broadening scope or choosing a schema/security direction by assumption → **Correct**: provide 3 options and wait for developer selection

## Context Compaction Priority

1. Project Overview (including structure)
2. Autonomy Guardrails
3. Backup Rules
4. Commit / PR Rules
5. Common Mistakes section
6. Security Requirements
7. Naming Conventions
8. Commit Convention
9. Tech Stack / Commands

## Detailed Guides (Skills)

Load the corresponding skill for in-depth guidance:

- **aspnet-arch** (`Codex/skills/aspnet-arch/SKILL.md`): Layer structure, DI strategy, middleware patterns
- **auth-security** (`Codex/skills/auth-security/SKILL.md`): Registration/login security, BCrypt, session management, email verification flow
- **backup-guide** (`Codex/skills/backup-guide/SKILL.md`): DB/code backup, rollback, and recoverability guardrails before risky work
- **efcore-guide** (`Codex/skills/efcore-guide/SKILL.md`): EF Core migrations, FluentAPI, N+1 prevention patterns
- **migration-guide** (`Codex/skills/migration-guide/SKILL.md`): EF Core migration planning, generation, review, rollback, script, and DB update guardrails
- **code-review** (`Codex/skills/code-review/SKILL.md`): Checklist-based automatic code review
- **save-changes** (`Codex/skills/save-changes/SKILL.md`): Safe Git status review, selective staging, verification, commit creation, and PR preparation
- **security-checklist** (`Codex/skills/security-checklist/SKILL.md`): Hardcoded secrets, SQL Injection, session vulnerability checks

## Subagents

Load project-specific subagent prompts from `Codex/agents/`:

- **planner** (`Codex/agents/planner.md`): Feature planning before implementation
- **implementer** (`Codex/agents/implementer.md`): Scoped C# / ASP.NET Core implementation
- **reviewer** (`Codex/agents/reviewer.md`): Checklist-based code review
- **git-manager** (`Codex/agents/git-manager.md`): Git status review, functional commit splitting, commit creation, and PR preparation
- **security-guardian** (`Codex/agents/security-guardian.md`): Deep auth/session/data-flow security audit
- **tester** (`Codex/agents/tester.md`): xUnit + Moq test writing and execution
- **inform-finder** (`Codex/agents/inform-finder.md`): Current external information lookup with sources
