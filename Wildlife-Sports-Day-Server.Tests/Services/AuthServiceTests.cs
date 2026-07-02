using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Wildlife_Sports_Day_Server.Dtos.Requests;
using Wildlife_Sports_Day_Server.Entities;
using Wildlife_Sports_Day_Server.Exceptions;
using Wildlife_Sports_Day_Server.Repositories;
using Wildlife_Sports_Day_Server.Services;
using Xunit;

namespace Wildlife_Sports_Day_Server.Tests.Services;

public class AuthServiceTests
{
    [Fact]
    public async Task SendVerificationEmailAsync_NewEmail_SavesHashedCodeAndSendsEmail()
    {
        var userRepository = new Mock<IUserRepository>();
        var codeRepository = new Mock<IEmailVerificationCodeRepository>();
        var emailSender = new Mock<IEmailSender>();
        EmailVerificationCode? savedCode = null;
        string? emailBody = null;

        userRepository.Setup(repository => repository.ExistsByEmailAsync("user@example.com")).ReturnsAsync(false);
        codeRepository
            .Setup(repository => repository.FindLatestByEmailAsync("user@example.com"))
            .ReturnsAsync((EmailVerificationCode?)null);
        codeRepository
            .Setup(repository => repository.SaveAsync(It.IsAny<EmailVerificationCode>()))
            .Callback<EmailVerificationCode>(code => savedCode = code)
            .ReturnsAsync((EmailVerificationCode code) => code);
        emailSender
            .Setup(sender => sender.SendAsync("user@example.com", It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string, string>((_, _, body) => emailBody = body)
            .Returns(Task.CompletedTask);

        var service = CreateService(userRepository, codeRepository, emailSender);

        await service.SendVerificationEmailAsync(new SendVerificationCodeRequest { Email = "user@example.com" });

        Assert.NotNull(savedCode);
        Assert.NotNull(emailBody);
        var rawCode = Regex.Match(emailBody, @"\b\d{6}\b").Value;
        Assert.Matches(@"^\d{6}$", rawCode);
        Assert.NotEqual(rawCode, savedCode.CodeHash);
        Assert.True(BCrypt.Net.BCrypt.Verify(rawCode, savedCode.CodeHash));
        Assert.Equal("user@example.com", savedCode.Email);
        Assert.False(savedCode.IsVerified);
        Assert.False(savedCode.IsUsed);
        codeRepository.Verify(repository => repository.InvalidateAllByEmailAsync("user@example.com"), Times.Once);
    }

    [Fact]
    public async Task SendVerificationEmailAsync_MixedCaseEmail_NormalizesEmailBeforeSaveAndSend()
    {
        var userRepository = new Mock<IUserRepository>();
        var codeRepository = new Mock<IEmailVerificationCodeRepository>();
        var emailSender = new Mock<IEmailSender>();
        EmailVerificationCode? savedCode = null;

        userRepository.Setup(repository => repository.ExistsByEmailAsync("user@example.com")).ReturnsAsync(false);
        codeRepository
            .Setup(repository => repository.FindLatestByEmailAsync("user@example.com"))
            .ReturnsAsync((EmailVerificationCode?)null);
        codeRepository
            .Setup(repository => repository.SaveAsync(It.IsAny<EmailVerificationCode>()))
            .Callback<EmailVerificationCode>(code => savedCode = code)
            .ReturnsAsync((EmailVerificationCode code) => code);
        emailSender
            .Setup(sender => sender.SendAsync("user@example.com", It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var service = CreateService(userRepository, codeRepository, emailSender);

        await service.SendVerificationEmailAsync(new SendVerificationCodeRequest { Email = " User@Example.COM " });

        Assert.NotNull(savedCode);
        Assert.Equal("user@example.com", savedCode.Email);
        userRepository.Verify(repository => repository.ExistsByEmailAsync("user@example.com"), Times.Once);
        codeRepository.Verify(repository => repository.InvalidateAllByEmailAsync("user@example.com"), Times.Once);
        emailSender.Verify(sender => sender.SendAsync("user@example.com", It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task SendVerificationEmailAsync_EmailSenderFails_MarksCodeUsedAndThrowsServerError()
    {
        var userRepository = new Mock<IUserRepository>();
        var codeRepository = new Mock<IEmailVerificationCodeRepository>();
        var emailSender = new Mock<IEmailSender>();
        EmailVerificationCode? savedCode = null;

        userRepository.Setup(repository => repository.ExistsByEmailAsync("user@example.com")).ReturnsAsync(false);
        codeRepository
            .Setup(repository => repository.FindLatestByEmailAsync("user@example.com"))
            .ReturnsAsync((EmailVerificationCode?)null);
        codeRepository
            .Setup(repository => repository.SaveAsync(It.IsAny<EmailVerificationCode>()))
            .Callback<EmailVerificationCode>(code => savedCode = code)
            .ReturnsAsync((EmailVerificationCode code) => code);
        emailSender
            .Setup(sender => sender.SendAsync("user@example.com", It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("SMTP failure"));

        var service = CreateService(userRepository, codeRepository, emailSender);

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            service.SendVerificationEmailAsync(new SendVerificationCodeRequest { Email = "user@example.com" }));

        Assert.Equal(StatusCodes.Status500InternalServerError, exception.StatusCode);
        Assert.Equal("인증 코드 발송에 실패했습니다.", exception.Message);
        Assert.NotNull(savedCode);
        Assert.True(savedCode.IsUsed);
        Assert.NotNull(savedCode.UsedAt);
        codeRepository.Verify(repository => repository.UpdateAsync(savedCode), Times.Once);
    }

    [Fact]
    public async Task SendVerificationEmailAsync_WithinCooldown_ThrowsTooManyRequests()
    {
        var userRepository = new Mock<IUserRepository>();
        var codeRepository = new Mock<IEmailVerificationCodeRepository>();
        var emailSender = new Mock<IEmailSender>();
        var latestCode = CreatePendingCode("123456", DateTime.UtcNow.AddMinutes(5));
        latestCode.CreatedAt = DateTime.UtcNow;

        userRepository.Setup(repository => repository.ExistsByEmailAsync("user@example.com")).ReturnsAsync(false);
        codeRepository.Setup(repository => repository.FindLatestByEmailAsync("user@example.com")).ReturnsAsync(latestCode);

        var service = CreateService(userRepository, codeRepository, emailSender);

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            service.SendVerificationEmailAsync(new SendVerificationCodeRequest { Email = "user@example.com" }));

        Assert.Equal(StatusCodes.Status429TooManyRequests, exception.StatusCode);
        Assert.Equal("인증 코드는 1분 후에 재발송할 수 있습니다.", exception.Message);
        codeRepository.Verify(repository => repository.InvalidateAllByEmailAsync(It.IsAny<string>()), Times.Never);
        codeRepository.Verify(repository => repository.SaveAsync(It.IsAny<EmailVerificationCode>()), Times.Never);
        emailSender.Verify(sender => sender.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task SendVerificationEmailAsync_DuplicateEmail_ThrowsConflict()
    {
        var userRepository = new Mock<IUserRepository>();
        var codeRepository = new Mock<IEmailVerificationCodeRepository>();
        var emailSender = new Mock<IEmailSender>();

        userRepository.Setup(repository => repository.ExistsByEmailAsync("user@example.com")).ReturnsAsync(true);

        var service = CreateService(userRepository, codeRepository, emailSender);

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            service.SendVerificationEmailAsync(new SendVerificationCodeRequest { Email = "user@example.com" }));

        Assert.Equal(StatusCodes.Status409Conflict, exception.StatusCode);
        Assert.Equal("이미 사용 중인 이메일입니다.", exception.Message);
    }

    [Fact]
    public async Task VerifyEmailCodeAsync_ValidCode_MarksCodeVerified()
    {
        var userRepository = new Mock<IUserRepository>();
        var codeRepository = new Mock<IEmailVerificationCodeRepository>();
        var emailSender = new Mock<IEmailSender>();
        var verificationCode = CreatePendingCode("123456", DateTime.UtcNow.AddMinutes(5));

        codeRepository.Setup(repository => repository.FindLatestByEmailAsync("user@example.com")).ReturnsAsync(verificationCode);

        var service = CreateService(userRepository, codeRepository, emailSender);

        await service.VerifyEmailCodeAsync(new VerifyEmailCodeRequest
        {
            Email = "user@example.com",
            Code = "123456"
        });

        Assert.True(verificationCode.IsVerified);
        Assert.NotNull(verificationCode.VerifiedAt);
        codeRepository.Verify(repository => repository.UpdateAsync(verificationCode), Times.Once);
    }

    [Fact]
    public async Task VerifyEmailCodeAsync_ExpiredCode_MarksCodeUsedAndThrowsBadRequest()
    {
        var userRepository = new Mock<IUserRepository>();
        var codeRepository = new Mock<IEmailVerificationCodeRepository>();
        var emailSender = new Mock<IEmailSender>();
        var verificationCode = CreatePendingCode("123456", DateTime.UtcNow.AddMinutes(-1));

        codeRepository.Setup(repository => repository.FindLatestByEmailAsync("user@example.com")).ReturnsAsync(verificationCode);

        var service = CreateService(userRepository, codeRepository, emailSender);

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            service.VerifyEmailCodeAsync(new VerifyEmailCodeRequest
            {
                Email = "user@example.com",
                Code = "123456"
            }));

        Assert.Equal(StatusCodes.Status400BadRequest, exception.StatusCode);
        Assert.Equal("인증 코드가 만료되었습니다.", exception.Message);
        Assert.True(verificationCode.IsUsed);
        Assert.NotNull(verificationCode.UsedAt);
        codeRepository.Verify(repository => repository.UpdateAsync(verificationCode), Times.Once);
    }

    [Fact]
    public async Task VerifyEmailCodeAsync_MismatchedCode_ThrowsBadRequest()
    {
        var userRepository = new Mock<IUserRepository>();
        var codeRepository = new Mock<IEmailVerificationCodeRepository>();
        var emailSender = new Mock<IEmailSender>();
        var verificationCode = CreatePendingCode("123456", DateTime.UtcNow.AddMinutes(5));

        codeRepository.Setup(repository => repository.FindLatestByEmailAsync("user@example.com")).ReturnsAsync(verificationCode);

        var service = CreateService(userRepository, codeRepository, emailSender);

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            service.VerifyEmailCodeAsync(new VerifyEmailCodeRequest
            {
                Email = "user@example.com",
                Code = "654321"
            }));

        Assert.Equal(StatusCodes.Status400BadRequest, exception.StatusCode);
        Assert.Equal("인증 코드가 올바르지 않습니다.", exception.Message);
        Assert.Equal(1, verificationCode.AttemptCount);
        codeRepository.Verify(repository => repository.UpdateAsync(verificationCode), Times.Once);
    }

    [Fact]
    public async Task VerifyEmailCodeAsync_LastAllowedMismatch_MarksCodeUsedAndThrowsTooManyRequests()
    {
        var userRepository = new Mock<IUserRepository>();
        var codeRepository = new Mock<IEmailVerificationCodeRepository>();
        var emailSender = new Mock<IEmailSender>();
        var verificationCode = CreatePendingCode("123456", DateTime.UtcNow.AddMinutes(5));
        verificationCode.AttemptCount = 4;

        codeRepository.Setup(repository => repository.FindLatestByEmailAsync("user@example.com")).ReturnsAsync(verificationCode);

        var service = CreateService(userRepository, codeRepository, emailSender);

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            service.VerifyEmailCodeAsync(new VerifyEmailCodeRequest
            {
                Email = "user@example.com",
                Code = "654321"
            }));

        Assert.Equal(StatusCodes.Status429TooManyRequests, exception.StatusCode);
        Assert.Equal("인증 코드 시도 횟수를 초과했습니다.", exception.Message);
        Assert.Equal(5, verificationCode.AttemptCount);
        Assert.True(verificationCode.IsUsed);
        Assert.NotNull(verificationCode.UsedAt);
        codeRepository.Verify(repository => repository.UpdateAsync(verificationCode), Times.Once);
    }

    [Fact]
    public async Task VerifyEmailCodeAsync_AttemptLimitExceededWithCorrectCode_ThrowsTooManyRequests()
    {
        var userRepository = new Mock<IUserRepository>();
        var codeRepository = new Mock<IEmailVerificationCodeRepository>();
        var emailSender = new Mock<IEmailSender>();
        var verificationCode = CreatePendingCode("123456", DateTime.UtcNow.AddMinutes(5));
        verificationCode.AttemptCount = 5;

        codeRepository.Setup(repository => repository.FindLatestByEmailAsync("user@example.com")).ReturnsAsync(verificationCode);

        var service = CreateService(userRepository, codeRepository, emailSender);

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            service.VerifyEmailCodeAsync(new VerifyEmailCodeRequest
            {
                Email = "user@example.com",
                Code = "123456"
            }));

        Assert.Equal(StatusCodes.Status429TooManyRequests, exception.StatusCode);
        Assert.Equal("인증 코드 시도 횟수를 초과했습니다.", exception.Message);
        Assert.True(verificationCode.IsUsed);
        codeRepository.Verify(repository => repository.UpdateAsync(verificationCode), Times.Once);
    }

    [Fact]
    public async Task RegisterAsync_UnverifiedEmail_ThrowsBadRequest()
    {
        var userRepository = new Mock<IUserRepository>();
        var codeRepository = new Mock<IEmailVerificationCodeRepository>();
        var emailSender = new Mock<IEmailSender>();

        userRepository.Setup(repository => repository.ExistsByEmailAsync("user@example.com")).ReturnsAsync(false);
        codeRepository
            .Setup(repository => repository.FindLatestByEmailAsync("user@example.com"))
            .ReturnsAsync((EmailVerificationCode?)null);

        var service = CreateService(userRepository, codeRepository, emailSender);

        var exception = await Assert.ThrowsAsync<AppException>(() => service.RegisterAsync(CreateRegisterRequest()));

        Assert.Equal(StatusCodes.Status400BadRequest, exception.StatusCode);
        Assert.Equal("이메일 인증이 완료되지 않았습니다.", exception.Message);
        userRepository.Verify(repository => repository.SaveAsync(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task RegisterAsync_VerifiedEmail_SavesUserWithPasswordHashAndConsumesCode()
    {
        var userRepository = new Mock<IUserRepository>();
        var codeRepository = new Mock<IEmailVerificationCodeRepository>();
        var emailSender = new Mock<IEmailSender>();
        var verificationCode = CreatePendingCode("123456", DateTime.UtcNow.AddMinutes(5));
        verificationCode.IsVerified = true;
        User? savedUser = null;

        userRepository.Setup(repository => repository.ExistsByEmailAsync("user@example.com")).ReturnsAsync(false);
        userRepository
            .Setup(repository => repository.SaveAsync(It.IsAny<User>()))
            .Callback<User>(user => savedUser = user)
            .ReturnsAsync((User user) =>
            {
                user.Id = 7;
                return user;
            });
        codeRepository.Setup(repository => repository.FindLatestByEmailAsync("user@example.com")).ReturnsAsync(verificationCode);

        var service = CreateService(userRepository, codeRepository, emailSender);

        var response = await service.RegisterAsync(CreateRegisterRequest());

        Assert.Equal(7, response.Id);
        Assert.Equal("user@example.com", response.Email);
        Assert.Equal("nickname", response.Nickname);
        Assert.NotNull(savedUser);
        Assert.NotEqual(CreateValidCredential(), savedUser.PasswordHash);
        Assert.True(BCrypt.Net.BCrypt.Verify(CreateValidCredential(), savedUser.PasswordHash));
        Assert.True(verificationCode.IsUsed);
        Assert.NotNull(verificationCode.UsedAt);
        codeRepository.Verify(repository => repository.UpdateAsync(verificationCode), Times.Once);
    }

    [Fact]
    public async Task RegisterAsync_PasswordConfirmationMismatch_ThrowsBadRequest()
    {
        var userRepository = new Mock<IUserRepository>();
        var codeRepository = new Mock<IEmailVerificationCodeRepository>();
        var emailSender = new Mock<IEmailSender>();

        var service = CreateService(userRepository, codeRepository, emailSender);

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            service.RegisterAsync(CreateRegisterRequest(CreateDifferentCredential())));

        Assert.Equal(StatusCodes.Status400BadRequest, exception.StatusCode);
        Assert.Equal("비밀번호가 일치하지 않습니다.", exception.Message);
        userRepository.Verify(repository => repository.ExistsByEmailAsync(It.IsAny<string>()), Times.Never);
    }

    private static AuthService CreateService(
        Mock<IUserRepository> userRepository,
        Mock<IEmailVerificationCodeRepository> codeRepository,
        Mock<IEmailSender> emailSender) =>
        new(
            userRepository.Object,
            codeRepository.Object,
            emailSender.Object,
            NullLogger<AuthService>.Instance);

    private static EmailVerificationCode CreatePendingCode(string rawCode, DateTime expiresAt) =>
        new()
        {
            Email = "user@example.com",
            CodeHash = BCrypt.Net.BCrypt.HashPassword(rawCode),
            ExpiresAt = expiresAt
        };

    private static RegisterRequest CreateRegisterRequest(string? confirmation = null)
    {
        var request = new RegisterRequest
        {
            Email = "user@example.com",
            Nickname = "nickname"
        };
        typeof(RegisterRequest)
            .GetProperty(nameof(RegisterRequest.Password))!
            .SetValue(request, CreateValidCredential());
        typeof(RegisterRequest)
            .GetProperty(nameof(RegisterRequest.ConfirmPassword))!
            .SetValue(request, confirmation ?? CreateValidCredential());
        return request;
    }

    private static string CreateValidCredential() =>
        string.Concat("A", "a", "123", "456", "!");

    private static string CreateDifferentCredential() =>
        string.Concat("B", "b", "654", "321", "!");
}
