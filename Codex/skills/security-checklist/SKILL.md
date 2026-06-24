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
  "DefaultConnection": "Host=localhost;Password=mypassword123"
}

// ❌ Never allowed — written directly in code
var password = "smtp_password_here";
```

### Correct Approach

```bash
# Development: User Secrets
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=...;Password=..."
dotnet user-secrets set "Gmail:AppPassword" "app-password"

# Production: environment variables
export ConnectionStrings__DefaultConnection="Host=...;Password=..."
export Gmail__AppPassword="app-password"
```

### Required .gitignore Entries

```gitignore
# User Secrets (just in case)
secrets.json

# Development config (may contain sensitive data)
appsettings.Development.json

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
user.Password = request.Password;

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
// Regenerate session after successful login
public async Task<LoginResponse> LoginAsync(LoginRequest request, HttpContext httpContext)
{
    // ... password verification ...

    // ✅ Clear existing session before saving new one
    await httpContext.Session.CommitAsync();
    httpContext.Session.Clear();

    httpContext.Session.SetInt32("UserId", user.Id);
    httpContext.Session.SetString("UserEmail", user.Email);
}
```

---

## 4. Email Verification Code Security

```csharp
// ❌ Forbidden — predictable code
var code = (DateTime.Now.Millisecond % 900000 + 100000).ToString();

// ✅ Cryptographically random (most secure)
var bytes = new byte[4];
RandomNumberGenerator.Fill(bytes);
var code = (Math.Abs(BitConverter.ToInt32(bytes)) % 900000 + 100000).ToString();

// ✅ Simple approach (sufficiently secure)
var code = Random.Shared.Next(100000, 999999).ToString();
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
❌ appsettings.Development.json missing from .gitignore

### 💉 SQL Injection
✅ No Raw SQL, using EF Core LINQ

### 🔐 Authentication Security
✅ BCrypt hashing in use
✅ User enumeration prevention message used
❌ Session regeneration after login missing

### 🍪 Session Security
✅ HttpOnly configured
❌ SameSite=Strict not set

---
**Summary**: .gitignore update, session regeneration, and SameSite configuration are required.
```
