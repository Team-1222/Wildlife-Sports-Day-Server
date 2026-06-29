---
name: aspnet-arch
description: ASP.NET Core architecture guidance for Wildlife Survival Server. Use for layer separation, dependency injection, controller-service-repository boundaries, middleware order, ApiResponse usage, and AppException handling patterns.
---

# aspnet-arch Skill

Detailed guide for ASP.NET Core layer structure, dependency injection (DI), and middleware patterns.

---

## Layer Structure & Dependency Direction

```
Controller → Service (Interface) → Repository (Interface) → EF Core DbContext
``` 

- Each layer depends **only on interfaces**, never on concrete implementations directly
- `Controller` only knows the `Service` interface and never calls `Repository` directly

### Correct DI Registration (Program.cs)

```csharp
// Register by layer
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IScoreService, ScoreService>();
builder.Services.AddScoped<IRankingService, RankingService>();

// Email sender
builder.Services.AddScoped<IEmailSender, GmailEmailSender>();

// EF Core
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Session
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
});
```

---

## Controller Pattern

```csharp
[ApiController]
[Route("api/[controller]")]
public class AuthController(IAuthService authService) : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<ApiResponse<RegisterResponse>>> Register(
        [FromBody] RegisterRequest request)
    {
        var result = await authService.RegisterAsync(request);
        return Ok(ApiResponse<RegisterResponse>.Success(result));
    }
}
```

**Rules:**
- Constructor injection (Primary Constructor recommended)
- Return type: `ActionResult<ApiResponse<T>>`
- No business logic — validate then delegate to Service only

---

## Service Pattern

```csharp
public interface IAuthService
{
    Task<RegisterResponse> RegisterAsync(RegisterRequest request);
    Task<LoginResponse> LoginAsync(LoginRequest request, HttpContext httpContext);
    Task SendVerificationEmailAsync(string email);
    Task VerifyEmailCodeAsync(string email, string code);
}

public class AuthService(
    IUserRepository userRepository,
    IEmailSender emailSender,
    ILogger<AuthService> logger) : IAuthService
{
    public async Task<RegisterResponse> RegisterAsync(RegisterRequest request)
    {
        // Business logic only — no direct DB access
    }
}
```

---

## Repository Pattern

```csharp
public interface IUserRepository
{
    Task<User?> FindByEmailAsync(string email);
    Task<User> SaveAsync(User user);
    Task<bool> ExistsByEmailAsync(string email);
}

public class UserRepository(AppDbContext dbContext) : IUserRepository
{
    public async Task<User?> FindByEmailAsync(string email)
        => await dbContext.Users.FirstOrDefaultAsync(u => u.Email == email);

    public async Task<User> SaveAsync(User user)
    {
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
        return user;
    }
}
```

**Rules:**
- `DbContext` is used only inside Repositories
- Return `null` when query result is not found (`FirstOrDefaultAsync`)
- No business logic

---

## Global Exception Handling Middleware

```csharp
public class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (AppException ex)
        {
            logger.LogWarning("Expected exception: {Message}", ex.Message);
            await WriteErrorResponse(context, ex.StatusCode, ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error occurred");
            await WriteErrorResponse(context, 500, "서버 내부 오류가 발생했습니다.");
        }
    }

    private static async Task WriteErrorResponse(HttpContext context, int statusCode, string message)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        var response = ApiResponse<object>.Fail(message);
        await context.Response.WriteAsJsonAsync(response);
    }
}
```

---

## ApiResponse Wrapper

```csharp
public class ApiResponse<T>
{
    public bool Success { get; init; }
    public T? Data { get; init; }
    public ErrorDetail? Error { get; init; }

    public static ApiResponse<T> Success(T data) =>
        new() { Success = true, Data = data };

    public static ApiResponse<T> Fail(string message, string? code = null) =>
        new() { Success = false, Error = new ErrorDetail(code ?? "ERROR", message) };
}

public record ErrorDetail(string Code, string Message);
```

---

## AppException

```csharp
public class AppException(string message, int statusCode = StatusCodes.Status400BadRequest)
    : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
```

---

## Middleware Registration Order (Program.cs)

```csharp
// Order matters!
app.UseMiddleware<ExceptionMiddleware>(); // 1. Exception handling (highest priority)
app.UseHttpsRedirection();               // 2. HTTPS redirect
app.UseRouting();                        // 3. Routing
app.UseAuthentication();                 // 4. Authentication
app.UseAuthorization();                  // 5. Authorization
app.UseSession();                        // 6. Session
app.MapControllers();                    // 7. Controller mapping
```
