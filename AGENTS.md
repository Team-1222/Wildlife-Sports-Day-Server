**한국어로 응답하고 작업해주세요.**

## Project

Wildlife Survival Server는 ASP.NET Core 기반 게임 백엔드입니다. 로그인, 회원가입, MailKit 이메일 인증, 점수 저장, 랭킹 기능을 제공합니다.

- Backend: C#, ASP.NET Core
- DB/ORM: PostgreSQL + EF Core
- Session: ASP.NET Core Session Middleware
- Security: BCrypt, User Secrets, environment variables
- Structure: `Controllers -> Services -> Repositories -> Infrastructure`

## Commands

- Build: `dotnet build`
- Run: `dotnet run`
- Add migration: `dotnet ef migrations add <Name>`
- Apply DB: `dotnet ef database update`
  단, DB 적용은 명시 승인 없이는 실행하지 않습니다.

## Core Rules

- 작업 범위를 사용자 요청 밖으로 넓히지 않습니다.
- 불필요한 리팩터링, 공개 API 변경, DB 스키마 변경, 인증/세션/보안 정책 변경, 의존성/런타임 설정 변경은 임의로 하지 않습니다.
- 중요한 결정은 최소 3가지 방향과 장단점을 제시하고 개발자 선택을 기다립니다.
- 코드 변경 전 기존 패턴을 먼저 확인하고, 프로젝트의 레이어 구조와 인터페이스 기반 DI를 유지합니다.
- 보안 값은 절대 하드코딩하지 않습니다. 개발은 User Secrets, 운영은 환경 변수를 사용합니다.
- `AppException` 메시지는 한국어 합쇼체, 마침표 포함, 동적 데이터 없이 작성합니다.
- 로깅은 영어 동사형 문장과 `ILogger` 구조화 로깅 `{}`을 사용합니다.
- 응답은 `ApiResponse<T>` 래퍼를 사용합니다.
- 모든 async 메서드는 `Async` 접미사를 사용하고 `async void`를 금지합니다.

## Skills

세부 규칙은 아래 스킬을 로드해 따릅니다.

- Architecture: `Codex/skills/aspnet-arch/SKILL.md`
- Auth/session/email/security: `Codex/skills/auth-security/SKILL.md`
- Security checklist: `Codex/skills/security-checklist/SKILL.md`
- EF Core/FluentAPI/query: `Codex/skills/efcore-guide/SKILL.md`
- Migration planning/review/update: `Codex/skills/migration-guide/SKILL.md`
- DB/code backup and rollback: `Codex/skills/backup-guide/SKILL.md`
- Code review: `Codex/skills/code-review/SKILL.md`
- Commit/PR/save workflow: `Codex/skills/save-changes/SKILL.md`

## Required Skill Triggers

- DB 엔티티, FluentAPI, DbContext, 마이그레이션을 건드리면 `efcore-guide`와 `migration-guide`를 로드합니다.
- `dotnet ef database update`, 롤백, 데이터 삭제, 컬럼/테이블 삭제, 수동 SQL 전에는 `backup-guide`와 `migration-guide`를 로드하고 승인받습니다.
- 인증, 비밀번호, 이메일 인증, 세션, 쿠키, 시크릿을 건드리면 `auth-security`와 `security-checklist`를 로드합니다.
- 커밋, 저장, 푸시, PR 작업은 `save-changes`를 로드합니다.
- 코드 리뷰 요청이나 코드 변경 후 검토는 `code-review`를 로드합니다.

## Commit / PR

- 커밋 제목 형식: `type: 설명`
- 커밋 본문 첫 줄에는 관련 이슈가 있으면 `#<이슈번호>`를 작성합니다. 예: `#123`
- 이슈 번호를 모르면 커밋 전 확인하고, 이슈가 없으면 본문 이슈 줄을 생략합니다.
- Type: `feat`, `fix`, `docs`, `refactor`, `test`, `ci`, `chore`
- 커밋 제목에는 scope를 넣지 않습니다. 도메인/영역은 설명에 자연스럽게 포함합니다.
- 기능/영역별로 세부 커밋합니다. 예: 스킬 폴더별, `AGENTS.md`/`Codex/CODEX.md`, 훅, 서브에이전트, 설정 변경을 분리합니다.
- 여러 스킬이 바뀌면 가능한 한 스킬 종류/폴더별로 1개씩 커밋합니다.
- 명시 요청 없이는 push, force-push, PR 생성, 리뷰어 요청, ready 전환, merge를 하지 않습니다.
- DB 영향 PR은 백업 상태와 롤백 경로를 포함합니다.

## Agents

- Planner: 큰 기능 전 설계. Entity/Repository/Service/Controller/DTO/Security/Commit 순서로 계획합니다.
- Implementer: 계획 또는 명확한 요청만 구현합니다. 불명확한 중요 결정은 3가지 선택지를 제시합니다.
- Reviewer: 변경 파일을 읽고 보안, SOLID, 레이어, async, 예외, 네이밍, 응답, 로깅, 커밋 규칙을 검토합니다.
- GitManager: 변경사항을 기능/영역별 커밋으로 나누고 PR 준비 규칙을 점검합니다.
- SecurityGuardian: 패턴 훅이 잡지 못하는 인증/세션/데이터 흐름 보안 문제를 검토합니다.
- Tester: xUnit + Moq, Given-When-Then, `MethodName_Scenario_ExpectedResult` 규칙을 따릅니다.
- InformFinder: 최신 외부 정보가 필요한 경우 웹 검색과 출처 URL을 제공합니다.

## Key Paths

- AppDbContext: `Wildlife-Sports-Day-Server/Infrastructure/AppDbContext.cs`
- Exception middleware: `Wildlife-Sports-Day-Server/Middleware/ExceptionMiddleware.cs`
- ApiResponse: `Wildlife-Sports-Day-Server/Dtos/Responses/ApiResponse.cs`
- AppException: `Wildlife-Sports-Day-Server/Exceptions/AppException.cs`
- Hook config: `.codex/hooks.json`
- Hooks: `Codex/hooks/*.mjs`
- Agents: `Codex/agents/`
- Skills: `Codex/skills/<skill-name>/SKILL.md`
