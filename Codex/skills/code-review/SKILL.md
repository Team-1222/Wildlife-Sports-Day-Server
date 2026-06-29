---
name: code-review
description: Checklist-based code review guidance for Wildlife Survival Server. Use when reviewing code changes for security, SOLID, layer separation, async patterns, exception messages, naming, ApiResponse usage, logging, and commit convention compliance.
---

# code-review Skill

When code changes occur, automatically review the checklist **in order**.  
Each item is marked as pass (✅) / fail (❌) / not applicable (➖).

---

## Review Checklist

### 1. Security (Highest Priority)

- [ ] No hardcoded passwords / API keys / ConnectionStrings
- [ ] No sensitive values in `appsettings.json` (structure only)
- [ ] Passwords hashed with BCrypt (no plaintext storage)
- [ ] No SQL Injection risk (no Raw SQL, using EF Core parameter binding)
- [ ] Email verification code has expiry time set (5 minutes)
- [ ] Session cookie: HttpOnly + Secure + SameSite=Strict

### 2. SOLID Principles

- [ ] **SRP**: Class/method has only one responsibility
- [ ] **OCP**: Structure is extensible without modifying existing code
- [ ] **LSP**: Implementation accurately fulfills interface contracts
- [ ] **ISP**: Interface does not force unnecessary methods
- [ ] **DIP**: Depends on interfaces, not concrete implementations

### 3. Layer Separation

- [ ] Dependency direction: Controller → Service interface → Repository interface
- [ ] No business logic in Controller
- [ ] No business logic in Repository
- [ ] `DbContext` not used directly outside of Repository

### 4. Async Patterns

- [ ] All async methods have `Async` suffix
- [ ] No `async void` → using `async Task`
- [ ] No `async` methods without `await`
- [ ] No blocking calls like `.Result` / `.Wait()`

### 5. Exception Handling

- [ ] `AppException` used directly (no subclasses)
- [ ] Exception messages: Korean 합쇼체 + period
- [ ] No dynamic data (IDs, names) in exception messages
- [ ] `try-catch` scope is not too broad

### 6. Naming Conventions

- [ ] Class/Interface: PascalCase
- [ ] Method: PascalCase
- [ ] Variable/Parameter: camelCase
- [ ] DTO: `XxxRequest` / `XxxResponse` suffix
- [ ] Interface: `I` prefix (`IAuthService`)

### 7. API Response

- [ ] All responses use `ApiResponse<T>` wrapper
- [ ] Correct HTTP status codes for both success and failure cases

### 8. Logging

- [ ] English verb-led sentences
- [ ] ILogger structured logging `{}` used
- [ ] No sensitive data in logs (passwords, verification codes)

### 9. Commit Convention

- [ ] Title format: `type: 한국어 설명`
- [ ] Types: `feat` / `fix` / `docs` / `refactor` / `test` / `ci` / `chore`
- [ ] No scope in the title; include the domain naturally in the description when useful
- [ ] Related issue is written in the body as `#<이슈번호>` when one exists
- [ ] No trailing period

---

## Review Output Format

```
## Code Review Result

### 🔒 Security
✅ No hardcoded sensitive information
✅ BCrypt password hashing
❌ Session cookie SameSite setting missing

### 🏗️ SOLID Principles
✅ Single Responsibility Principle followed
❌ AuthService handles both email sending and user saving — consider separating

### 🔄 Async Patterns
✅ All async methods have Async suffix
✅ No async void

---
**Summary**: Session cookie SameSite configuration and AuthService responsibility separation are needed.
```
