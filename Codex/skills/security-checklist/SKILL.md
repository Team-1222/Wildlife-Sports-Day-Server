---
name: security-checklist
description: Security audit checklist for Wildlife Survival Server. Use to inspect hardcoded secrets, SQL injection risks, authentication weaknesses, session cookie configuration, email verification code security, and required security headers.
---

# security-checklist Skill

Security vulnerability checklist covering hardcoded secrets, SQL Injection, session vulnerabilities, and more.

---

## 1. Secret Management Check

### Dangerous Patterns (Fix immediately)

```csharp
// ❌ Never allowed — hardcoded in appsettings.json or code
"ConnectionStrings": {
  "DefaultConnection": "Host=localhost;Password=<password>"
}

// ❌ Never allowed — written directly in code
var password = "changeme";
```

### Correct Approach

```bash
# Development: User Secrets
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=...;Password=<password>"
dotnet user-secrets set "Gmail:AppPassword" "app-password"

# Production: environment variables
export ConnectionStrings__DefaultConnection="Host=...;Password=<password>"
export Gmail__AppPassword="app-password"
```

### Required .gitignore Entries

```gitignore
# User Secrets (just in case)
secrets.json

# Local override config files that may contain sensitive values
appsettings.Local.json
appsettings.*.Local.json

# Do not blanket-ignore appsettings.Development.json.
# Keep safe development defaults trackable; put secrets in User Secrets,
# environment variables, or explicitly ignored local override files.

# Environment variable files
.env
.env.local
```

---

## 2. SQL Injection Check

### Dangerous Pattern

```csharp
// ❌ Forbidden — variable inserted directly into Raw SQL
var users = dbContext.Users
    .FromSqlRaw($"SELECT * FROM users WHERE email = '{email}'")
    .ToList();
```

### Correct Approach

```csharp
// ✅ EF Core LINQ (safest)
var user = await dbContext.Users
    .FirstOrDefaultAsync(u => u.Email == email);

// ✅ When Raw SQL is absolutely necessary — use parameter binding
var user = await dbContext.Users
    .FromSqlRaw("SELECT * FROM users WHERE email = {0}", email)
    .FirstOrDefaultAsync();
```

---

## 3. Authentication / Authorization Vulnerability Check

### Password Security

```csharp
// ❌ Forbidden — plaintext storage
user.PlainTextPassword = request.Password;

// ❌ Forbidden — MD5/SHA1 (vulnerable)
user.PasswordHash = MD5.HashData(Encoding.UTF8.GetBytes(request.Password));

// ✅ BCrypt (recommended, default work factor 11)
user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

// ✅ Verification
bool isValid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
```

### User Enumeration Attack Prevention

```csharp
// ❌ Forbidden — reveals which field was wrong
throw new AppException("이메일이 존재하지 않습니다.", 401);
throw new AppException("비밀번호가 틀렸습니다.", 401);

// ✅ Correct — unified message
throw new AppException("이메일 또는 비밀번호가 올바르지 않습니다.", StatusCodes.Status401Unauthorized);
```

### Session Fixation Attack Prevention

```csharp
// Do not treat ISession.Clear() or CommitAsync() as session identifier rotation.
// Fresh sign-in/session reissue must be implemented by the configured auth/session strategy.
public async Task<LoginResponse> LoginAsync(LoginRequest request, HttpContext httpContext)
{
    // ... password verification ...

    // If the app uses cookie authentication, issue a fresh auth cookie after validation.
    // If the app uses a custom server-side session store, rotate the session key there.
    // ISession.Clear() only removes session entries; it must not be documented as cookie rotation.

    httpContext.Session.SetInt32("UserId", user.Id);
    httpContext.Session.SetString("UserEmail", user.Email);
}
```

`ISession.Clear()` is useful for logout or removing existing session values, but it does not guarantee a new session cookie or session identifier. For session fixation mitigation, define a fresh sign-in/session reissue flow in the authentication design instead of relying on `Clear()` or `CommitAsync()`.

---

## 4. Email Verification Code Security

```csharp
// ❌ Forbidden — predictable code
var code = (DateTime.Now.Millisecond % 900000 + 100000).ToString();

// ✅ Required — cryptographically secure 6-digit code
var numericCode = RandomNumberGenerator.GetInt32(100000, 1000000);
while (numericCode % 111111 == 0)
{
    numericCode = RandomNumberGenerator.GetInt32(100000, 1000000);
}
var code = numericCode.ToString();
```

### Rate Limiting for Code Resend

```csharp
// Prevent resending within 1 minute to the same email
var lastCode = await emailCodeRepository.FindLatestByEmailAsync(email);
if (lastCode is not null && lastCode.CreatedAt > DateTime.UtcNow.AddMinutes(-1))
    throw new AppException("인증 코드는 1분 후에 재발송할 수 있습니다.", StatusCodes.Status429TooManyRequests);
```

---

## 5. Session Cookie Security Configuration

```csharp
// ✅ Required settings
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.Name = ".Wildlife.Session";
    options.Cookie.HttpOnly = true;        // XSS prevention
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;  // HTTPS only
    options.Cookie.SameSite = SameSiteMode.Strict;            // CSRF prevention
});
```

---

## 6. Security HTTP Headers

```csharp
// Add to Program.cs
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
    await next();
});
```

---

## Check Result Output Format

```
## Security Check Result

### 🔑 Secret Management
✅ No sensitive information in appsettings.json
✅ User Secrets usage confirmed
❌ Local override config containing secrets is not covered by .gitignore

### 💉 SQL Injection
✅ No Raw SQL, using EF Core LINQ

### 🔐 Authentication Security
✅ BCrypt hashing in use
✅ User enumeration prevention message used
❌ Fresh sign-in/session reissue flow missing for session fixation mitigation

### 🍪 Session Security
✅ HttpOnly configured
❌ SameSite=Strict not set

---
**Summary**: .gitignore local override coverage, fresh sign-in/session reissue flow, and SameSite configuration are required.
```
