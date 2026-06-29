---
name: auth-security
description: Registration and login security guidance for Wildlife Survival Server. Use for BCrypt password hashing, session management, MailKit Gmail SMTP setup, email verification code flow, and authentication-related ASP.NET Core implementation decisions.
---

# auth-security Skill

Detailed guide for registration / login security, BCrypt, session management, and MailKit email verification flow.

---

## Email Verification Flow

```
1. POST /api/auth/send-code   → generate 6-digit code → save hash to DB (5-min expiry) → send raw code via Gmail
2. POST /api/auth/verify-code → find latest code by email → compare submitted code with hash → throw AppException on expiry/mismatch → mark as verified
3. POST /api/auth/register    → confirm email verified → BCrypt hash password → save user
```

---

## Email Verification Code Entity

```csharp
public class EmailVerificationCode
{
    public int Id { get; set; }
    public string Email { get; set; } = null!;
    public string CodeHash { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
    public bool IsVerified { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

**Index configuration (FluentAPI):**
```csharp
modelBuilder.Entity<EmailVerificationCode>()
    .HasIndex(e => e.Email);
```

---

## RegisterEmailCodeStatus Enum

```csharp
public enum RegisterEmailCodeStatus
{
    Pending,    // Code sent, not yet verified
    Verified,   // Verification complete
    Expired     // Expired
}
```

---

## Email Code Send Service

```csharp
public async Task SendVerificationEmailAsync(string email)
{
    // 1. Invalidate existing codes
    await emailCodeRepository.InvalidateAllByEmailAsync(email);

    // 2. Generate 6-digit code
    var code = GenerateVerificationCode();
    var codeHash = HashVerificationCode(code);

    // 3. Save hashed code to DB (5-minute expiry)
    var verificationCode = new EmailVerificationCode
    {
        Email = email,
        CodeHash = codeHash,
        ExpiresAt = DateTime.UtcNow.AddMinutes(5),
        IsVerified = false
    };
    await emailCodeRepository.SaveAsync(verificationCode);

    // 4. Send raw code by email only. Never persist or log the raw code.
    await emailSender.SendAsync(email, "Wildlife Survival 이메일 인증", BuildEmailBody(code));

    logger.LogInformation("Sent verification code to {Email}", email);
}

private static string HashVerificationCode(string code)
    => BCrypt.Net.BCrypt.HashPassword(code);

private static string GenerateVerificationCode()
{
    int code = Random.Shared.Next(100000, 1000000);     
    while(code % 111111 == 0)//동일한 6자리 숫자는 제외
    {
        code = Random.Shared.Next(100000, 1000000);
    }
    return code.ToString();
}
```

---

## Code Verification Service

```csharp
public async Task VerifyEmailCodeAsync(string email, string code)
{
    // Lookup by email only. The submitted raw code is verified against the stored hash.
    var record = await emailCodeRepository.FindLatestByEmailAsync(email)
        ?? throw new AppException("인증 코드를 찾을 수 없습니다.", StatusCodes.Status404NotFound);

    if (record.ExpiresAt < DateTime.UtcNow)
        throw new AppException("인증 코드가 만료되었습니다.", StatusCodes.Status400BadRequest);

    if (!BCrypt.Net.BCrypt.Verify(code, record.CodeHash))
        throw new AppException("인증 코드가 올바르지 않습니다.", StatusCodes.Status400BadRequest);

    record.IsVerified = true;
    await emailCodeRepository.UpdateAsync(record);
}
```

**Security notes:**
- Store only `CodeHash` in the database; never store the raw email verification code.
- Use the raw code only for the outgoing email body and the user's verification request.
- Look up the latest pending record by email, then compare the submitted raw code with `CodeHash` using `BCrypt.Verify`.
- Do not log verification codes or include them in exception messages.

---

## Registration Service

```csharp
public async Task<RegisterResponse> RegisterAsync(RegisterRequest request)
{
    // 1. Check email duplicate
    if (await userRepository.ExistsByEmailAsync(request.Email))
        throw new AppException("이미 사용 중인 이메일입니다.", StatusCodes.Status409Conflict);

    // 2. Confirm email verification complete
    var codeRecord = await emailCodeRepository.FindLatestByEmailAsync(request.Email);
    if (codeRecord is null || !codeRecord.IsVerified)
        throw new AppException("이메일 인증이 완료되지 않았습니다.", StatusCodes.Status400BadRequest);

    // 3. Hash password
    var hashedPassword = BCrypt.Net.BCrypt.HashPassword(request.Password);

    // 4. Save user
    var user = new User
    {
        Email = request.Email,
        Nickname = request.Nickname,
        PasswordHash = hashedPassword,
        CreatedAt = DateTime.UtcNow
    };
    var savedUser = await userRepository.SaveAsync(user);

    logger.LogInformation("Registered new user with email {Email}", request.Email);

    return new RegisterResponse(savedUser.Id, savedUser.Email, savedUser.Nickname);
}
```

---

## Login Service (Cookie-based)

Required namespaces:

```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
```

```csharp
public async Task<LoginResponse> LoginAsync(LoginRequest request, HttpContext httpContext)
{
    // 1. Find user
    var user = await userRepository.FindByEmailAsync(request.Email)
        ?? throw new AppException("이메일 또는 비밀번호가 올바르지 않습니다.", StatusCodes.Status401Unauthorized);

    // 2. Verify password
    if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        throw new AppException("이메일 또는 비밀번호가 올바르지 않습니다.", StatusCodes.Status401Unauthorized);

    // 3. Issue authentication cookie
    var claims = new List<Claim>
    {
        new(ClaimTypes.NameIdentifier, user.Id.ToString()),
        new(ClaimTypes.Email, user.Email),
        new(ClaimTypes.Name, user.Nickname)
    };

    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    var principal = new ClaimsPrincipal(identity);

    await httpContext.SignInAsync(
        CookieAuthenticationDefaults.AuthenticationScheme,
        principal,
        new AuthenticationProperties
        {
            IsPersistent = false,
            IssuedUtc = DateTimeOffset.UtcNow
        });

    logger.LogInformation("User logged in: {Email}", user.Email);

    return new LoginResponse(user.Id, user.Email, user.Nickname);
}
```

**Security notes:**
- On login failure, use "이메일 또는 비밀번호가 올바르지 않습니다." — never reveal which field was wrong (prevents enumeration attacks)
- BCrypt Verify uses timing-safe comparison
- Login/authentication must not store authenticated identity in `ISession`
- Use ASP.NET Core cookie authentication and `SignInAsync` to issue a fresh authentication cookie after password validation
- Read the authenticated user from `HttpContext.User` claims in authenticated requests

---

## Logout

```csharp
public async Task LogoutAsync(HttpContext httpContext)
{
    await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
}
```

---

## MailKit Gmail SMTP Configuration

### User Secrets setup (development)

```bash
dotnet user-secrets set "Gmail:Address" "your-email@gmail.com"
dotnet user-secrets set "Gmail:AppPassword" "your-app-password"
```

### appsettings.json (structure only, no values)

```json
{
  "Gmail": {
    "Address": "",
    "AppPassword": ""
  }
}
```

### GmailEmailSender Implementation

```csharp
public class GmailEmailSender(IConfiguration configuration, ILogger<GmailEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(string to, string subject, string body)
    {
        var gmailAddress = configuration["Gmail:Address"]
            ?? throw new InvalidOperationException("Gmail address is not configured.");
        var gmailCredential = configuration["Gmail:AppPassword"]
            ?? throw new InvalidOperationException("Gmail app password is not configured.");

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(gmailAddress));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new TextPart("html") { Text = body };

        using var client = new SmtpClient();
        await client.ConnectAsync("smtp.gmail.com", 587, SecureSocketOptions.StartTls);
        await client.AuthenticateAsync(gmailAddress, gmailCredential);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);

        logger.LogInformation("Email sent to {Recipient}", to);
    }
}
```

---

## Cookie Authentication Configuration (Program.cs)

```csharp
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = ".Wildlife.Auth";
        options.Cookie.HttpOnly = true;       // Block JS access to auth cookie (XSS prevention)
        options.Cookie.IsEssential = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;  // HTTPS only
        options.Cookie.SameSite = SameSiteMode.Strict;            // CSRF prevention
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
    });

builder.Services.AddAuthorization();

app.UseAuthentication();
app.UseAuthorization();
```

Do not use `ISession` as the login/authentication store. Keep server-side session state out of login and authenticated identity flows; use authentication cookies and claims instead.

## Optional Session Configuration (Non-auth app state only)

```csharp
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;       // Block JS access to session cookie (XSS prevention)
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;  // HTTPS only
    options.Cookie.SameSite = SameSiteMode.Strict;            // CSRF prevention
});
```

Use `ISession` only for non-authentication application state when it is genuinely needed.

---

## Password Validation (Request DTO)

```csharp
public class RegisterRequest
{
    [Required(ErrorMessage = "이메일은 필수입니다.")]
    [EmailAddress(ErrorMessage = "올바른 이메일 형식이 아닙니다.")]
    public string Email { get; init; } = null!;

    [Required(ErrorMessage = "닉네임은 필수입니다.")]
    [StringLength(20, MinimumLength = 2, ErrorMessage = "닉네임은 2~20자여야 합니다.")]
    public string Nickname { get; init; } = null!;

    [Required(ErrorMessage = "비밀번호는 필수입니다.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "비밀번호는 최소 8자여야 합니다.")]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[\W_]).+$",
        ErrorMessage = "비밀번호는 대소문자, 숫자, 특수문자를 포함해야 합니다.")]
    public string Password { get; init; } = null!;
}
```
