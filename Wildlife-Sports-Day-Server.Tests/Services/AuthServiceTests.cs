using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
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
            .Setup(repository => repository.FindLatestActiveByEmailAsync("user@example.com"))
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
        Assert.Equal(EmailVerificationCodeStatus.Pending, savedCode.Status);
        Assert.Null(savedCode.UnavailableAt);
        codeRepository.Verify(repository => repository.RevokeActiveByEmailExceptAsync("user@example.com", savedCode.Id), Times.Once);
    }

    [Fact]
    public async Task SendVerificationEmailAsync_MixedCaseEmail_NormalizesEmailBeforeSaveAndSend()
    {
        var userRepository = new Mock<IUserRepository>();
        var codeRepository = new Mock<IEmailVerificationCodeRepository>();
        var emailSender = new Mock<IEmailSender>();
        var existingCode = CreatePendingCode("654321", DateTime.UtcNow.AddMinutes(3));
        existingCode.CreatedAt = DateTime.UtcNow.AddMinutes(-2);
        EmailVerificationCode? savedCode = null;

        userRepository.Setup(repository => repository.ExistsByEmailAsync("user@example.com")).ReturnsAsync(false);
        codeRepository
            .Setup(repository => repository.FindLatestActiveByEmailAsync("user@example.com"))
            .ReturnsAsync(existingCode);
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
        codeRepository.Verify(repository => repository.RevokeActiveByEmailExceptAsync("user@example.com", savedCode.Id), Times.Once);
        emailSender.Verify(sender => sender.SendAsync("user@example.com", It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task SendVerificationEmailAsync_EmailSenderFails_MarksCodeSendFailedAndThrowsServerError()
    {
        var userRepository = new Mock<IUserRepository>();
        var codeRepository = new Mock<IEmailVerificationCodeRepository>();
        var emailSender = new Mock<IEmailSender>();
        var existingCode = CreatePendingCode("654321", DateTime.UtcNow.AddMinutes(3));
        existingCode.CreatedAt = DateTime.UtcNow.AddMinutes(-2);
        EmailVerificationCode? savedCode = null;

        userRepository.Setup(repository => repository.ExistsByEmailAsync("user@example.com")).ReturnsAsync(false);
        codeRepository
            .Setup(repository => repository.FindLatestActiveByEmailAsync("user@example.com"))
            .ReturnsAsync(existingCode);
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
        Assert.Equal(EmailVerificationCodeStatus.SendFailed, savedCode.Status);
        Assert.NotNull(savedCode.UnavailableAt);
        Assert.Equal(EmailVerificationCodeStatus.Pending, existingCode.Status);
        Assert.Null(existingCode.UnavailableAt);
        codeRepository.Verify(repository => repository.UpdateAsync(savedCode), Times.Once);
        codeRepository.Verify(
            repository => repository.RevokeActiveByEmailExceptAsync(It.IsAny<string>(), It.IsAny<int>()),
            Times.Never);
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
        codeRepository.Setup(repository => repository.FindLatestActiveByEmailAsync("user@example.com")).ReturnsAsync(latestCode);

        var service = CreateService(userRepository, codeRepository, emailSender);

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            service.SendVerificationEmailAsync(new SendVerificationCodeRequest { Email = "user@example.com" }));

        Assert.Equal(StatusCodes.Status429TooManyRequests, exception.StatusCode);
        Assert.Equal("인증 코드는 1분 후에 재발송할 수 있습니다.", exception.Message);
        codeRepository.Verify(
            repository => repository.RevokeActiveByEmailExceptAsync(It.IsAny<string>(), It.IsAny<int>()),
            Times.Never);
        codeRepository.Verify(repository => repository.SaveAsync(It.IsAny<EmailVerificationCode>()), Times.Never);
        emailSender.Verify(sender => sender.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task SendVerificationEmailAsync_PreviousSendFailedCode_SendsEmailWithoutCooldown()
    {
        var userRepository = new Mock<IUserRepository>();
        var codeRepository = new Mock<IEmailVerificationCodeRepository>();
        var emailSender = new Mock<IEmailSender>();
        EmailVerificationCode? savedCode = null;

        userRepository.Setup(repository => repository.ExistsByEmailAsync("user@example.com")).ReturnsAsync(false);
        codeRepository
            .Setup(repository => repository.FindLatestActiveByEmailAsync("user@example.com"))
            .ReturnsAsync((EmailVerificationCode?)null);
        codeRepository
            .Setup(repository => repository.SaveAsync(It.IsAny<EmailVerificationCode>()))
            .Callback<EmailVerificationCode>(code => savedCode = code)
            .ReturnsAsync((EmailVerificationCode code) => code);
        emailSender
            .Setup(sender => sender.SendAsync("user@example.com", It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var service = CreateService(userRepository, codeRepository, emailSender);

        await service.SendVerificationEmailAsync(new SendVerificationCodeRequest { Email = "user@example.com" });

        Assert.NotNull(savedCode);
        Assert.Equal(EmailVerificationCodeStatus.Pending, savedCode.Status);
        emailSender.Verify(sender => sender.SendAsync("user@example.com", It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        codeRepository.Verify(repository => repository.RevokeActiveByEmailExceptAsync("user@example.com", savedCode.Id), Times.Once);
    }

    [Fact]
    public async Task SendVerificationEmailAsync_DuplicateEmail_ReturnsMessageWithoutSendingEmail()
    {
        var userRepository = new Mock<IUserRepository>();
        var codeRepository = new Mock<IEmailVerificationCodeRepository>();
        var emailSender = new Mock<IEmailSender>();

        userRepository.Setup(repository => repository.ExistsByEmailAsync("user@example.com")).ReturnsAsync(true);

        var service = CreateService(userRepository, codeRepository, emailSender);

        var response = await service.SendVerificationEmailAsync(new SendVerificationCodeRequest { Email = "user@example.com" });

        Assert.Equal("인증 코드가 발송되었습니다.", response.Message);
        codeRepository.Verify(repository => repository.SaveAsync(It.IsAny<EmailVerificationCode>()), Times.Never);
        emailSender.Verify(sender => sender.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task LoginAsync_ValidCredential_SignsInUser()
    {
        var userRepository = new Mock<IUserRepository>();
        var codeRepository = new Mock<IEmailVerificationCodeRepository>();
        var emailSender = new Mock<IEmailSender>();
        var authenticationService = new TestAuthenticationService();
        var httpContext = CreateHttpContext(authenticationService);
        var user = CreateUser();

        userRepository.Setup(repository => repository.FindByNicknameAsync("nickname")).ReturnsAsync(user);

        var service = CreateService(userRepository, codeRepository, emailSender);

        var response = await service.LoginAsync(CreateLoginRequest(" nickname "), httpContext);

        Assert.Equal(7, response.UserId);
        Assert.Equal("nickname", response.Username);
        Assert.Equal("user@example.com", response.Email);
        Assert.Equal("Player", response.Role);
        Assert.NotNull(authenticationService.SignedInPrincipal);
        Assert.Equal("7", authenticationService.SignedInPrincipal.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        Assert.Equal("nickname", authenticationService.SignedInPrincipal.FindFirst(ClaimTypes.Name)?.Value);
        userRepository.Verify(repository => repository.FindByNicknameAsync("nickname"), Times.Once);
    }

    [Fact]
    public async Task LoginAsync_UnknownNickname_ThrowsUnauthorized()
    {
        var userRepository = new Mock<IUserRepository>();
        var codeRepository = new Mock<IEmailVerificationCodeRepository>();
        var emailSender = new Mock<IEmailSender>();
        var authenticationService = new TestAuthenticationService();
        var httpContext = CreateHttpContext(authenticationService);

        userRepository.Setup(repository => repository.FindByNicknameAsync("unknown")).ReturnsAsync((User?)null);

        var service = CreateService(userRepository, codeRepository, emailSender);

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            service.LoginAsync(CreateLoginRequest("unknown"), httpContext));

        Assert.Equal(StatusCodes.Status401Unauthorized, exception.StatusCode);
        Assert.Equal("닉네임 또는 비밀번호가 올바르지 않습니다.", exception.Message);
        Assert.Null(authenticationService.SignedInPrincipal);
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_ThrowsUnauthorized()
    {
        var userRepository = new Mock<IUserRepository>();
        var codeRepository = new Mock<IEmailVerificationCodeRepository>();
        var emailSender = new Mock<IEmailSender>();
        var authenticationService = new TestAuthenticationService();
        var httpContext = CreateHttpContext(authenticationService);

        userRepository.Setup(repository => repository.FindByNicknameAsync("nickname")).ReturnsAsync(CreateUser());

        var service = CreateService(userRepository, codeRepository, emailSender);

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            service.LoginAsync(CreateLoginRequest("nickname", CreateDifferentCredential()), httpContext));

        Assert.Equal(StatusCodes.Status401Unauthorized, exception.StatusCode);
        Assert.Equal("닉네임 또는 비밀번호가 올바르지 않습니다.", exception.Message);
        Assert.Null(authenticationService.SignedInPrincipal);
    }

    [Fact]
    public async Task LoginAsync_TooManyFailures_ThrowsTooManyRequests()
    {
        var userRepository = new Mock<IUserRepository>();
        var codeRepository = new Mock<IEmailVerificationCodeRepository>();
        var emailSender = new Mock<IEmailSender>();
        var authenticationService = new TestAuthenticationService();
        var httpContext = CreateHttpContext(authenticationService);

        userRepository.Setup(repository => repository.FindByNicknameAsync("locked-user")).ReturnsAsync((User?)null);

        var service = CreateService(userRepository, codeRepository, emailSender);
        var request = CreateLoginRequest("locked-user");

        for (var attempt = 0; attempt < 5; attempt++)
        {
            await Assert.ThrowsAsync<AppException>(() => service.LoginAsync(request, httpContext));
        }

        var exception = await Assert.ThrowsAsync<AppException>(() => service.LoginAsync(request, httpContext));

        Assert.Equal(StatusCodes.Status429TooManyRequests, exception.StatusCode);
        Assert.Equal("로그인 시도 횟수를 초과했습니다.", exception.Message);
        Assert.Null(authenticationService.SignedInPrincipal);
    }

    [Fact]
    public async Task VerifyEmailCodeAsync_ValidCode_MarksCodeVerified()
    {
        var userRepository = new Mock<IUserRepository>();
        var codeRepository = new Mock<IEmailVerificationCodeRepository>();
        var emailSender = new Mock<IEmailSender>();
        var verificationCode = CreatePendingCode("123456", DateTime.UtcNow.AddMinutes(5));
        verificationCode.Id = 11;
        var httpContext = CreateHttpContext();

        codeRepository.Setup(repository => repository.FindLatestActiveByEmailAsync("user@example.com")).ReturnsAsync(verificationCode);

        var service = CreateService(userRepository, codeRepository, emailSender);

        await service.VerifyEmailCodeAsync(new VerifyEmailCodeRequest
        {
            Email = "user@example.com",
            Code = "123456"
        }, httpContext);

        Assert.Equal(EmailVerificationCodeStatus.Verified, verificationCode.Status);
        Assert.NotNull(verificationCode.VerifiedAt);
        Assert.Equal("user@example.com", httpContext.Session.GetString("EmailVerification.VerifiedEmail"));
        Assert.Equal(11, httpContext.Session.GetInt32("EmailVerification.VerifiedCodeId"));
        codeRepository.Verify(repository => repository.UpdateAsync(verificationCode), Times.Once);
    }

    [Fact]
    public async Task VerifyEmailCodeAsync_ExpiredCode_MarksCodeExpiredAndThrowsBadRequest()
    {
        var userRepository = new Mock<IUserRepository>();
        var codeRepository = new Mock<IEmailVerificationCodeRepository>();
        var emailSender = new Mock<IEmailSender>();
        var verificationCode = CreatePendingCode("123456", DateTime.UtcNow.AddMinutes(-1));
        var httpContext = CreateHttpContext();

        codeRepository.Setup(repository => repository.FindLatestActiveByEmailAsync("user@example.com")).ReturnsAsync(verificationCode);

        var service = CreateService(userRepository, codeRepository, emailSender);

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            service.VerifyEmailCodeAsync(new VerifyEmailCodeRequest
            {
                Email = "user@example.com",
                Code = "123456"
            }, httpContext));

        Assert.Equal(StatusCodes.Status400BadRequest, exception.StatusCode);
        Assert.Equal("인증 코드가 만료되었습니다.", exception.Message);
        Assert.Equal(EmailVerificationCodeStatus.Expired, verificationCode.Status);
        Assert.NotNull(verificationCode.UnavailableAt);
        codeRepository.Verify(repository => repository.UpdateAsync(verificationCode), Times.Once);
    }

    [Fact]
    public async Task VerifyEmailCodeAsync_MismatchedCode_ThrowsBadRequest()
    {
        var userRepository = new Mock<IUserRepository>();
        var codeRepository = new Mock<IEmailVerificationCodeRepository>();
        var emailSender = new Mock<IEmailSender>();
        var verificationCode = CreatePendingCode("123456", DateTime.UtcNow.AddMinutes(5));
        verificationCode.Id = 12;
        var httpContext = CreateHttpContext();

        codeRepository.Setup(repository => repository.FindLatestActiveByEmailAsync("user@example.com")).ReturnsAsync(verificationCode);
        codeRepository
            .Setup(repository => repository.IncrementAttemptCountAsync(verificationCode.Id, 5))
            .Callback<int, int>((_, _) => verificationCode.AttemptCount++)
            .ReturnsAsync(verificationCode);

        var service = CreateService(userRepository, codeRepository, emailSender);

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            service.VerifyEmailCodeAsync(new VerifyEmailCodeRequest
            {
                Email = "user@example.com",
                Code = "654321"
            }, httpContext));

        Assert.Equal(StatusCodes.Status400BadRequest, exception.StatusCode);
        Assert.Equal("인증 코드가 올바르지 않습니다.", exception.Message);
        Assert.Equal(1, verificationCode.AttemptCount);
        codeRepository.Verify(repository => repository.IncrementAttemptCountAsync(verificationCode.Id, 5), Times.Once);
        codeRepository.Verify(repository => repository.UpdateAsync(verificationCode), Times.Never);
    }

    [Fact]
    public async Task VerifyEmailCodeAsync_UnavailableStatus_ThrowsBadRequest()
    {
        var userRepository = new Mock<IUserRepository>();
        var codeRepository = new Mock<IEmailVerificationCodeRepository>();
        var emailSender = new Mock<IEmailSender>();
        var verificationCode = CreatePendingCode("123456", DateTime.UtcNow.AddMinutes(5));
        verificationCode.Status = EmailVerificationCodeStatus.Revoked;
        var httpContext = CreateHttpContext();

        codeRepository.Setup(repository => repository.FindLatestActiveByEmailAsync("user@example.com")).ReturnsAsync(verificationCode);

        var service = CreateService(userRepository, codeRepository, emailSender);

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            service.VerifyEmailCodeAsync(new VerifyEmailCodeRequest
            {
                Email = "user@example.com",
                Code = "123456"
            }, httpContext));

        Assert.Equal(StatusCodes.Status400BadRequest, exception.StatusCode);
        Assert.Equal("사용할 수 없는 인증 코드입니다.", exception.Message);
        codeRepository.Verify(repository => repository.UpdateAsync(verificationCode), Times.Never);
    }

    [Fact]
    public async Task VerifyEmailCodeAsync_LastAllowedMismatch_MarksCodeAttemptLimitExceededAndThrowsTooManyRequests()
    {
        var userRepository = new Mock<IUserRepository>();
        var codeRepository = new Mock<IEmailVerificationCodeRepository>();
        var emailSender = new Mock<IEmailSender>();
        var verificationCode = CreatePendingCode("123456", DateTime.UtcNow.AddMinutes(5));
        verificationCode.Id = 13;
        verificationCode.AttemptCount = 4;
        var httpContext = CreateHttpContext();

        codeRepository.Setup(repository => repository.FindLatestActiveByEmailAsync("user@example.com")).ReturnsAsync(verificationCode);
        codeRepository
            .Setup(repository => repository.IncrementAttemptCountAsync(verificationCode.Id, 5))
            .Callback<int, int>((_, _) =>
            {
                verificationCode.AttemptCount++;
                verificationCode.Status = EmailVerificationCodeStatus.AttemptLimitExceeded;
                verificationCode.UnavailableAt = DateTime.UtcNow;
            })
            .ReturnsAsync(verificationCode);

        var service = CreateService(userRepository, codeRepository, emailSender);

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            service.VerifyEmailCodeAsync(new VerifyEmailCodeRequest
            {
                Email = "user@example.com",
                Code = "654321"
            }, httpContext));

        Assert.Equal(StatusCodes.Status429TooManyRequests, exception.StatusCode);
        Assert.Equal("인증 코드 시도 횟수를 초과했습니다.", exception.Message);
        Assert.Equal(5, verificationCode.AttemptCount);
        Assert.Equal(EmailVerificationCodeStatus.AttemptLimitExceeded, verificationCode.Status);
        Assert.NotNull(verificationCode.UnavailableAt);
        codeRepository.Verify(repository => repository.IncrementAttemptCountAsync(verificationCode.Id, 5), Times.Once);
        codeRepository.Verify(repository => repository.UpdateAsync(verificationCode), Times.Never);
    }

    [Fact]
    public async Task VerifyEmailCodeAsync_AttemptLimitExceededWithCorrectCode_ThrowsTooManyRequests()
    {
        var userRepository = new Mock<IUserRepository>();
        var codeRepository = new Mock<IEmailVerificationCodeRepository>();
        var emailSender = new Mock<IEmailSender>();
        var verificationCode = CreatePendingCode("123456", DateTime.UtcNow.AddMinutes(5));
        verificationCode.AttemptCount = 5;
        var httpContext = CreateHttpContext();

        codeRepository.Setup(repository => repository.FindLatestActiveByEmailAsync("user@example.com")).ReturnsAsync(verificationCode);

        var service = CreateService(userRepository, codeRepository, emailSender);

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            service.VerifyEmailCodeAsync(new VerifyEmailCodeRequest
            {
                Email = "user@example.com",
                Code = "123456"
            }, httpContext));

        Assert.Equal(StatusCodes.Status429TooManyRequests, exception.StatusCode);
        Assert.Equal("인증 코드 시도 횟수를 초과했습니다.", exception.Message);
        Assert.Equal(EmailVerificationCodeStatus.AttemptLimitExceeded, verificationCode.Status);
        Assert.NotNull(verificationCode.UnavailableAt);
        codeRepository.Verify(repository => repository.UpdateAsync(verificationCode), Times.Once);
    }

    [Fact]
    public async Task RegisterAsync_UnverifiedEmail_ThrowsBadRequest()
    {
        var userRepository = new Mock<IUserRepository>();
        var codeRepository = new Mock<IEmailVerificationCodeRepository>();
        var emailSender = new Mock<IEmailSender>();
        var httpContext = CreateHttpContext();

        userRepository.Setup(repository => repository.ExistsByEmailAsync("user@example.com")).ReturnsAsync(false);
        userRepository.Setup(repository => repository.ExistsByNicknameAsync("nickname")).ReturnsAsync(false);
        codeRepository
            .Setup(repository => repository.FindLatestActiveByEmailAsync("user@example.com"))
            .ReturnsAsync((EmailVerificationCode?)null);

        var service = CreateService(userRepository, codeRepository, emailSender);

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            service.RegisterAsync(CreateRegisterRequest(), httpContext));

        Assert.Equal(StatusCodes.Status400BadRequest, exception.StatusCode);
        Assert.Equal("이메일 인증이 완료되지 않았습니다.", exception.Message);
        userRepository.Verify(repository => repository.ExistsByEmailAsync(It.IsAny<string>()), Times.Never);
        codeRepository.Verify(repository => repository.FindByIdAsync(It.IsAny<int>()), Times.Never);
        userRepository.Verify(repository => repository.SaveIfUniqueAsync(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task RegisterAsync_VerifiedEmail_SavesUserWithPasswordHashAndConsumesCode()
    {
        var userRepository = new Mock<IUserRepository>();
        var codeRepository = new Mock<IEmailVerificationCodeRepository>();
        var emailSender = new Mock<IEmailSender>();
        var verificationCode = CreatePendingCode("123456", DateTime.UtcNow.AddMinutes(-1));
        verificationCode.Id = 21;
        verificationCode.Status = EmailVerificationCodeStatus.Verified;
        verificationCode.VerifiedAt = DateTime.UtcNow;
        var httpContext = CreateVerifiedEmailHttpContext(verificationCode.Id);
        User? savedUser = null;

        userRepository.Setup(repository => repository.ExistsByEmailAsync("user@example.com")).ReturnsAsync(false);
        userRepository.Setup(repository => repository.ExistsByNicknameAsync("nickname")).ReturnsAsync(false);
        userRepository
            .Setup(repository => repository.SaveIfUniqueAsync(It.IsAny<User>()))
            .Callback<User>(user => savedUser = user)
            .ReturnsAsync((User user) =>
            {
                user.Id = 7;
                return user;
            });
        codeRepository.Setup(repository => repository.FindByIdAsync(verificationCode.Id)).ReturnsAsync(verificationCode);

        var service = CreateService(userRepository, codeRepository, emailSender);

        var response = await service.RegisterAsync(CreateRegisterRequest(), httpContext);

        Assert.Equal(7, response.UserId);
        Assert.Equal("user@example.com", response.Email);
        Assert.Equal("nickname", response.Username);
        Assert.Equal("Player", response.Role);
        Assert.NotEqual(default, response.CreatedAtUtc);
        Assert.NotNull(savedUser);
        Assert.Equal("nickname", savedUser.Nickname);
        Assert.NotEqual(CreateValidCredential(), savedUser.PasswordHash);
        Assert.True(BCrypt.Net.BCrypt.Verify(CreateValidCredential(), savedUser.PasswordHash));
        Assert.Equal(EmailVerificationCodeStatus.Consumed, verificationCode.Status);
        Assert.NotNull(verificationCode.UnavailableAt);
        Assert.Null(httpContext.Session.GetString("EmailVerification.VerifiedEmail"));
        Assert.Null(httpContext.Session.GetInt32("EmailVerification.VerifiedCodeId"));
        codeRepository.Verify(repository => repository.UpdateAsync(verificationCode), Times.Once);
    }

    [Fact]
    public async Task RegisterAsync_DuplicateEmail_ThrowsConflict()
    {
        var userRepository = new Mock<IUserRepository>();
        var codeRepository = new Mock<IEmailVerificationCodeRepository>();
        var emailSender = new Mock<IEmailSender>();
        var httpContext = CreateVerifiedEmailHttpContext(25);

        userRepository.Setup(repository => repository.ExistsByEmailAsync("user@example.com")).ReturnsAsync(true);

        var service = CreateService(userRepository, codeRepository, emailSender);

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            service.RegisterAsync(CreateRegisterRequest(), httpContext));

        Assert.Equal(StatusCodes.Status409Conflict, exception.StatusCode);
        Assert.Equal("이미 사용 중인 이메일입니다.", exception.Message);
        userRepository.Verify(repository => repository.ExistsByNicknameAsync(It.IsAny<string>()), Times.Never);
        codeRepository.Verify(repository => repository.FindByIdAsync(It.IsAny<int>()), Times.Never);
        userRepository.Verify(repository => repository.SaveIfUniqueAsync(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task RegisterAsync_DuplicateDuringSave_ThrowsConflictAndKeepsCodeVerified()
    {
        var userRepository = new Mock<IUserRepository>();
        var codeRepository = new Mock<IEmailVerificationCodeRepository>();
        var emailSender = new Mock<IEmailSender>();
        var verificationCode = CreatePendingCode("123456", DateTime.UtcNow.AddMinutes(5));
        verificationCode.Id = 22;
        verificationCode.Status = EmailVerificationCodeStatus.Verified;
        verificationCode.VerifiedAt = DateTime.UtcNow;
        var httpContext = CreateVerifiedEmailHttpContext(verificationCode.Id);

        userRepository.Setup(repository => repository.ExistsByEmailAsync("user@example.com")).ReturnsAsync(false);
        userRepository.Setup(repository => repository.ExistsByNicknameAsync("nickname")).ReturnsAsync(false);
        userRepository
            .Setup(repository => repository.SaveIfUniqueAsync(It.IsAny<User>()))
            .ReturnsAsync((User?)null);
        codeRepository.Setup(repository => repository.FindByIdAsync(verificationCode.Id)).ReturnsAsync(verificationCode);

        var service = CreateService(userRepository, codeRepository, emailSender);

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            service.RegisterAsync(CreateRegisterRequest(), httpContext));

        Assert.Equal(StatusCodes.Status409Conflict, exception.StatusCode);
        Assert.Equal("이미 사용 중인 이메일 또는 닉네임입니다.", exception.Message);
        Assert.Equal(EmailVerificationCodeStatus.Verified, verificationCode.Status);
        Assert.Null(verificationCode.UnavailableAt);
        codeRepository.Verify(repository => repository.UpdateAsync(It.IsAny<EmailVerificationCode>()), Times.Never);
    }

    [Fact]
    public async Task RegisterAsync_ExpiredVerifiedSignupWindow_MarksCodeExpiredAndThrowsBadRequest()
    {
        var userRepository = new Mock<IUserRepository>();
        var codeRepository = new Mock<IEmailVerificationCodeRepository>();
        var emailSender = new Mock<IEmailSender>();
        var verificationCode = CreatePendingCode("123456", DateTime.UtcNow.AddMinutes(5));
        verificationCode.Id = 23;
        verificationCode.Status = EmailVerificationCodeStatus.Verified;
        verificationCode.VerifiedAt = DateTime.UtcNow.AddMinutes(-6);
        var httpContext = CreateVerifiedEmailHttpContext(verificationCode.Id);

        userRepository.Setup(repository => repository.ExistsByEmailAsync("user@example.com")).ReturnsAsync(false);
        userRepository.Setup(repository => repository.ExistsByNicknameAsync("nickname")).ReturnsAsync(false);
        codeRepository.Setup(repository => repository.FindByIdAsync(verificationCode.Id)).ReturnsAsync(verificationCode);

        var service = CreateService(userRepository, codeRepository, emailSender);

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            service.RegisterAsync(CreateRegisterRequest(), httpContext));

        Assert.Equal(StatusCodes.Status400BadRequest, exception.StatusCode);
        Assert.Equal("인증 코드가 만료되었습니다.", exception.Message);
        Assert.Equal(EmailVerificationCodeStatus.Expired, verificationCode.Status);
        Assert.NotNull(verificationCode.UnavailableAt);
        Assert.Null(httpContext.Session.GetString("EmailVerification.VerifiedEmail"));
        Assert.Null(httpContext.Session.GetInt32("EmailVerification.VerifiedCodeId"));
        codeRepository.Verify(repository => repository.UpdateAsync(verificationCode), Times.Once);
        userRepository.Verify(repository => repository.SaveIfUniqueAsync(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task RegisterAsync_DuplicateNickname_ThrowsConflict()
    {
        var userRepository = new Mock<IUserRepository>();
        var codeRepository = new Mock<IEmailVerificationCodeRepository>();
        var emailSender = new Mock<IEmailSender>();
        var httpContext = CreateVerifiedEmailHttpContext(24);

        userRepository.Setup(repository => repository.ExistsByEmailAsync("user@example.com")).ReturnsAsync(false);
        userRepository.Setup(repository => repository.ExistsByNicknameAsync("nickname")).ReturnsAsync(true);

        var service = CreateService(userRepository, codeRepository, emailSender);

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            service.RegisterAsync(CreateRegisterRequest(), httpContext));

        Assert.Equal(StatusCodes.Status409Conflict, exception.StatusCode);
        Assert.Equal("이미 사용 중인 닉네임입니다.", exception.Message);
        codeRepository.Verify(repository => repository.FindLatestActiveByEmailAsync(It.IsAny<string>()), Times.Never);
        userRepository.Verify(repository => repository.SaveIfUniqueAsync(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task RegisterAsync_PasswordConfirmationMismatch_ThrowsBadRequest()
    {
        var userRepository = new Mock<IUserRepository>();
        var codeRepository = new Mock<IEmailVerificationCodeRepository>();
        var emailSender = new Mock<IEmailSender>();
        var httpContext = CreateHttpContext();

        var service = CreateService(userRepository, codeRepository, emailSender);

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            service.RegisterAsync(CreateRegisterRequest(CreateDifferentCredential()), httpContext));

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

    private static DefaultHttpContext CreateHttpContext(TestAuthenticationService authenticationService)
    {
        var services = new ServiceCollection()
            .AddSingleton<IAuthenticationService>(authenticationService)
            .BuildServiceProvider();

        return new DefaultHttpContext
        {
            RequestServices = services,
            Session = new TestSession()
        };
    }

    private static DefaultHttpContext CreateHttpContext() =>
        new()
        {
            Session = new TestSession()
        };

    private static DefaultHttpContext CreateVerifiedEmailHttpContext(int verificationCodeId)
    {
        var httpContext = CreateHttpContext();
        httpContext.Session.SetString("EmailVerification.VerifiedEmail", "user@example.com");
        httpContext.Session.SetInt32("EmailVerification.VerifiedCodeId", verificationCodeId);
        return httpContext;
    }

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

    private static LoginRequest CreateLoginRequest(string nickname, string? credential = null)
    {
        var request = new LoginRequest { Nickname = nickname };
        typeof(LoginRequest)
            .GetProperty(nameof(LoginRequest.Password))!
            .SetValue(request, credential ?? CreateValidCredential());
        return request;
    }

    private static string CreateValidCredential() =>
        string.Concat("A", "a", "123", "456", "!");

    private static string CreateDifferentCredential() =>
        string.Concat("B", "b", "654", "321", "!");

    private static User CreateUser() =>
        new()
        {
            Id = 7,
            Email = "user@example.com",
            Nickname = "nickname",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(CreateValidCredential()),
            CreatedAt = DateTime.UtcNow
        };

    private sealed class TestAuthenticationService : IAuthenticationService
    {
        public ClaimsPrincipal? SignedInPrincipal { get; private set; }

        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) =>
            Task.FromResult(AuthenticateResult.NoResult());

        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) =>
            Task.CompletedTask;

        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) =>
            Task.CompletedTask;

        public Task SignInAsync(
            HttpContext context,
            string? scheme,
            ClaimsPrincipal principal,
            AuthenticationProperties? properties)
        {
            SignedInPrincipal = principal;
            return Task.CompletedTask;
        }

        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) =>
            Task.CompletedTask;
    }

    private sealed class TestSession : ISession
    {
        private readonly Dictionary<string, byte[]> values = new();

        public bool IsAvailable => true;
        public string Id => "test-session";
        public IEnumerable<string> Keys => values.Keys;

        public Task LoadAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task CommitAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public bool TryGetValue(string key, out byte[] value) =>
            values.TryGetValue(key, out value!);

        public void Set(string key, byte[] value) =>
            values[key] = value;

        public void Remove(string key) =>
            values.Remove(key);

        public void Clear() =>
            values.Clear();
    }
}
