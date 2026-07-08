using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http.Features;
using Wildlife_Sports_Day_Server.Dtos.Requests;
using Wildlife_Sports_Day_Server.Dtos.Responses;
using Wildlife_Sports_Day_Server.Entities;
using Wildlife_Sports_Day_Server.Exceptions;
using Wildlife_Sports_Day_Server.Repositories;

namespace Wildlife_Sports_Day_Server.Services;

public class AuthService(
    IUserRepository userRepository,
    IEmailVerificationCodeRepository emailVerificationCodeRepository,
    IEmailSender emailSender,
    ILogger<AuthService> logger) : IAuthService
{
    private const int VerificationCodeMinutes = 5;//
    private const int VerifiedSignupMinutes = 5;//인증 기간
    private const int ResendCooldownSeconds = 60;//전송 대기 시간
    private const int MaxVerificationAttempts = 5;//이메일 인증 횟수
    private const string DefaultUserRole = "Player";

    public async Task<MessageResponse> SendVerificationEmailAsync(SendVerificationCodeRequest request)
    {
        var normalizedEmail = NormalizeEmail(request.Email);

        if (await userRepository.ExistsByEmailAsync(normalizedEmail))
        {
            throw new AppException("이미 사용 중인 이메일입니다.", StatusCodes.Status409Conflict);
        }

        var latestCode = await emailVerificationCodeRepository.FindLatestActiveByEmailAsync(normalizedEmail);
        if (latestCode is not null && latestCode.CreatedAt > DateTime.UtcNow.AddSeconds(-ResendCooldownSeconds))
        {
            throw new AppException("인증 코드는 1분 후에 재발송할 수 있습니다.", StatusCodes.Status429TooManyRequests);
        }

        var rawCode = GenerateVerificationCode();
        var verificationCode = new EmailVerificationCode
        {
            Email = normalizedEmail,
            CodeHash = BCrypt.Net.BCrypt.HashPassword(rawCode),
            ExpiresAt = DateTime.UtcNow.AddMinutes(VerificationCodeMinutes)
        };

        await emailVerificationCodeRepository.SaveAsync(verificationCode);
        try
        {
            await emailSender.SendAsync(
                normalizedEmail,
                "Wildlife Survival 이메일 인증",
                BuildEmailBody(rawCode, verificationCode.ExpiresAt));
        }
        catch (Exception exception)
        {
            verificationCode.Status = EmailVerificationCodeStatus.SendFailed;
            verificationCode.UnavailableAt = DateTime.UtcNow;
            await emailVerificationCodeRepository.UpdateAsync(verificationCode);
            logger.LogError(
                "Failed to send verification email for verification code {EmailVerificationCodeId} with exception type {ExceptionType}",
                verificationCode.Id,
                exception.GetType().Name);
            throw new AppException("인증 코드 발송에 실패했습니다.", StatusCodes.Status500InternalServerError);
        }

        await emailVerificationCodeRepository.RevokeActiveByEmailExceptAsync(normalizedEmail, verificationCode.Id);

        logger.LogInformation(
            "Sent verification email for verification code {EmailVerificationCodeId}",
            verificationCode.Id);

        return new MessageResponse { Message = "인증 코드가 발송되었습니다." };
    }

    public async Task<MessageResponse> VerifyEmailCodeAsync(VerifyEmailCodeRequest request)
    {
        var normalizedEmail = NormalizeEmail(request.Email);
        var verificationCode = await emailVerificationCodeRepository.FindLatestActiveByEmailAsync(normalizedEmail);
        if (verificationCode is null)
        {
            throw new AppException("인증 코드를 찾을 수 없습니다.", StatusCodes.Status404NotFound);
        }

        if (verificationCode.AttemptCount >= MaxVerificationAttempts)
        {
            verificationCode.Status = EmailVerificationCodeStatus.AttemptLimitExceeded;
            verificationCode.UnavailableAt ??= DateTime.UtcNow;
            await emailVerificationCodeRepository.UpdateAsync(verificationCode);
            throw new AppException("인증 코드 시도 횟수를 초과했습니다.", StatusCodes.Status429TooManyRequests);
        }

        if (verificationCode.Status is EmailVerificationCodeStatus.Verified)
        {
            throw new AppException("이미 인증된 코드입니다.", StatusCodes.Status400BadRequest);
        }

        if (verificationCode.Status is not EmailVerificationCodeStatus.Pending)
        {
            throw new AppException("사용할 수 없는 인증 코드입니다.", StatusCodes.Status400BadRequest);
        }

        if (verificationCode.ExpiresAt < DateTime.UtcNow)
        {
            verificationCode.Status = EmailVerificationCodeStatus.Expired;
            verificationCode.UnavailableAt = DateTime.UtcNow;
            await emailVerificationCodeRepository.UpdateAsync(verificationCode);
            throw new AppException("인증 코드가 만료되었습니다.", StatusCodes.Status400BadRequest);
        }

        if (!BCrypt.Net.BCrypt.Verify(request.Code, verificationCode.CodeHash))
        {
            verificationCode.AttemptCount++;
            if (verificationCode.AttemptCount >= MaxVerificationAttempts)
            {
                verificationCode.Status = EmailVerificationCodeStatus.AttemptLimitExceeded;
                verificationCode.UnavailableAt = DateTime.UtcNow;
            }

            await emailVerificationCodeRepository.UpdateAsync(verificationCode);

            if (verificationCode.Status is EmailVerificationCodeStatus.AttemptLimitExceeded)
            {
                throw new AppException("인증 코드 시도 횟수를 초과했습니다.", StatusCodes.Status429TooManyRequests);
            }

            throw new AppException("인증 코드가 올바르지 않습니다.", StatusCodes.Status400BadRequest);
        }

        verificationCode.Status = EmailVerificationCodeStatus.Verified;
        verificationCode.VerifiedAt = DateTime.UtcNow;
        await emailVerificationCodeRepository.UpdateAsync(verificationCode);

        logger.LogInformation(
            "Verified email code {EmailVerificationCodeId}",
            verificationCode.Id);

        return new MessageResponse { Message = "이메일 인증이 완료되었습니다." };
    }

    public async Task<RegisterResponse> RegisterAsync(RegisterRequest request)
    {
        var normalizedEmail = NormalizeEmail(request.Email);
        var normalizedNickname = NormalizeNickname(request.Nickname);

        if (request.Password != request.ConfirmPassword)
        {
            throw new AppException("비밀번호가 일치하지 않습니다.", StatusCodes.Status400BadRequest);
        }

        if (await userRepository.ExistsByEmailAsync(normalizedEmail))
        {
            throw new AppException("이미 사용 중인 이메일입니다.", StatusCodes.Status409Conflict);
        }

        if (await userRepository.ExistsByNicknameAsync(normalizedNickname))
        {
            throw new AppException("이미 사용 중인 닉네임입니다.", StatusCodes.Status409Conflict);
        }

        var verificationCode = await emailVerificationCodeRepository.FindLatestActiveByEmailAsync(normalizedEmail);
        if (verificationCode is null || verificationCode.Status is not EmailVerificationCodeStatus.Verified)
        {
            throw new AppException("이메일 인증이 완료되지 않았습니다.", StatusCodes.Status400BadRequest);
        }

        var now = DateTime.UtcNow;
        if (verificationCode.VerifiedAt is null
            || verificationCode.VerifiedAt.Value.AddMinutes(VerifiedSignupMinutes) < now)
        {
            verificationCode.Status = EmailVerificationCodeStatus.Expired;
            verificationCode.UnavailableAt = now;
            await emailVerificationCodeRepository.UpdateAsync(verificationCode);
            throw new AppException("인증 코드가 만료되었습니다.", StatusCodes.Status400BadRequest);
        }

        var user = new User
        {
            Email = normalizedEmail,
            Nickname = normalizedNickname,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            CreatedAt = DateTime.UtcNow
        };

        var savedUser = await userRepository.SaveIfUniqueAsync(user)
            ?? throw new AppException("이미 사용 중인 이메일 또는 닉네임입니다.", StatusCodes.Status409Conflict);

        verificationCode.Status = EmailVerificationCodeStatus.Consumed;
        verificationCode.UnavailableAt = DateTime.UtcNow;
        await emailVerificationCodeRepository.UpdateAsync(verificationCode);

        logger.LogInformation("Registered new user {UserId}", savedUser.Id);

        return new RegisterResponse
        {
            UserId = savedUser.Id,
            Username = savedUser.Nickname,
            Email = savedUser.Email,
            Role = DefaultUserRole,
            CreatedAtUtc = savedUser.CreatedAt
        };
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request, HttpContext httpContext)
    {
        var normalizedNickname = NormalizeNickname(request.Nickname);
        var user = await userRepository.FindByNicknameAsync(normalizedNickname);
        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            throw new AppException("닉네임 또는 비밀번호가 올바르지 않습니다.", StatusCodes.Status401Unauthorized);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.Nickname),
            new(ClaimTypes.Role, DefaultUserRole)
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

        var guestBestScore = httpContext.Features.Get<ISessionFeature>()?.Session.GetInt32(GuestSessionKeys.BestScore);
        if (guestBestScore is not null)
        {
            // TODO: 점수 저장 기능 구현 시 게스트 임시 점수를 사용자 정식 기록으로 저장합니다.
        }

        logger.LogInformation("Logged in user {UserId}", user.Id);

        return new LoginResponse
        {
            UserId = user.Id,
            Username = user.Nickname,
            Email = user.Email,
            Role = DefaultUserRole
        };
    }

    private static string NormalizeEmail(string email) =>
        email.Trim().ToLowerInvariant();

    private static string NormalizeNickname(string nickname) =>
        nickname.Trim();

    private static string GenerateVerificationCode()
    {
        var code = RandomNumberGenerator.GetInt32(100000, 1000000);
        while (code % 111111 == 0)
        {
            code = RandomNumberGenerator.GetInt32(100000, 1000000);
        }

        return code.ToString();
    }

    private static string BuildEmailBody(string code, DateTime expiresAt)
    {
        var encodedCode = HtmlEncoder.Default.Encode(code);
        var expiresAtText = HtmlEncoder.Default.Encode(expiresAt.ToString("yyyy-MM-dd HH:mm:ss 'UTC'"));
        return $"""
               <p>Wildlife Survival 이메일 인증 코드입니다.</p>
               <p><strong>{encodedCode}</strong></p>
               <p>만료 시간: {expiresAtText}</p>
               <p>이 코드는 {VerificationCodeMinutes}분 후에 만료됩니다.</p>
               """;
    }
}
