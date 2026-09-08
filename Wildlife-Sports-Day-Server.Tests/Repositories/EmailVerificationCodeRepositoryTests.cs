using Microsoft.EntityFrameworkCore;
using Wildlife_Sports_Day_Server.Entities;
using Wildlife_Sports_Day_Server.Repositories;
using Wildlife_Sports_Day_Server.Tests.Infrastructure;
using Xunit;

namespace Wildlife_Sports_Day_Server.Tests.Repositories;

public class EmailVerificationCodeRepositoryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RevokeOlderActiveByEmailAsync_OverlappingSends_KeepsNewestCode(bool newerCompletesFirst)
    {
        // Given: 발송 요청 두 개가 모두 저장되었으며 SMTP 완료 순서가 달라질 수 있습니다.
        await using var database = await RelationalTestDatabase.CreateAsync();
        await using var firstContext = database.CreateContext();
        await using var secondContext = database.CreateContext();
        var firstRepository = new EmailVerificationCodeRepository(firstContext);
        var secondRepository = new EmailVerificationCodeRepository(secondContext);
        var olderCode = await firstRepository.SaveAsync(CreateCode());
        var newerCode = CreateCode();
        newerCode.CreatedAt = olderCode.CreatedAt;
        await secondRepository.SaveAsync(newerCode);

        // When: 같은 생성 시각에서도 ID로 순서를 정하고 이전 요청은 최신 코드를 폐기하지 않습니다.
        if (newerCompletesFirst)
        {
            await secondRepository.RevokeOlderActiveByEmailAsync(newerCode.Email, newerCode.Id);
            await firstRepository.RevokeOlderActiveByEmailAsync(olderCode.Email, olderCode.Id);
        }
        else
        {
            await firstRepository.RevokeOlderActiveByEmailAsync(olderCode.Email, olderCode.Id);
            await secondRepository.RevokeOlderActiveByEmailAsync(newerCode.Email, newerCode.Id);
        }

        // Then
        await using var readContext = database.CreateContext();
        var activeCodes = await readContext.EmailVerificationCodes
            .Where(code => code.Status == EmailVerificationCodeStatus.Pending).ToListAsync();
        Assert.Equal(newerCode.Id, Assert.Single(activeCodes).Id);
        Assert.Equal(EmailVerificationCodeStatus.Revoked,
            (await readContext.EmailVerificationCodes.FindAsync(olderCode.Id))!.Status);
    }

    [Fact]
    public async Task FindLatestSentByEmailAsync_LockedCodeFollowedBySendFailure_ReturnsLockedCode()
    {
        // Given
        await using var database = await RelationalTestDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var repository = new EmailVerificationCodeRepository(context);
        var lockedCode = CreateCode();
        lockedCode.Status = EmailVerificationCodeStatus.AttemptLimitExceeded;
        lockedCode.CreatedAt = DateTime.UtcNow.AddSeconds(-10);
        await repository.SaveAsync(lockedCode);
        var failedCode = CreateCode();
        failedCode.Status = EmailVerificationCodeStatus.SendFailed;
        await repository.SaveAsync(failedCode);

        // When
        var result = await repository.FindLatestSentByEmailAsync(lockedCode.Email);

        // Then
        Assert.NotNull(result);
        Assert.Equal(lockedCode.Id, result.Id);
    }

    [Theory]
    [InlineData(EmailVerificationCodeStatus.Revoked)]
    [InlineData(EmailVerificationCodeStatus.AttemptLimitExceeded)]
    [InlineData(EmailVerificationCodeStatus.Consumed)]
    [InlineData(EmailVerificationCodeStatus.Expired)]
    [InlineData(EmailVerificationCodeStatus.Verified)]
    public async Task TryVerifyAsync_StateChangedAfterRead_PreservesLatestState(EmailVerificationCodeStatus status)
    {
        // Given: A가 읽은 뒤 B가 같은 코드를 변경합니다.
        await using var database = await RelationalTestDatabase.CreateAsync();
        await using var firstContext = database.CreateContext();
        var firstRepository = new EmailVerificationCodeRepository(firstContext);
        var code = await firstRepository.SaveAsync(CreateCode());
        await firstRepository.FindByIdAsync(code.Id);
        await using var secondContext = database.CreateContext();
        var secondRepository = new EmailVerificationCodeRepository(secondContext);
        var current = (await secondRepository.FindByIdAsync(code.Id))!;
        current.Status = status;
        current.AttemptCount = 5;
        await secondRepository.UpdateAsync(current);

        // When
        var verified = await firstRepository.TryVerifyAsync(code.Id, 5);

        // Then
        Assert.False(verified);
        await using var readContext = database.CreateContext();
        var stored = await readContext.EmailVerificationCodes.SingleAsync();
        Assert.Equal(status, stored.Status);
        Assert.Equal(5, stored.AttemptCount);
    }

    [Theory]
    [InlineData(5, 5)]
    [InlineData(0, -1)]
    public async Task TryVerifyAsync_LimitReachedOrExpired_RejectsVerification(int attempts, int expiryMinutes)
    {
        // Given
        await using var database = await RelationalTestDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var repository = new EmailVerificationCodeRepository(context);
        var code = CreateCode();
        code.AttemptCount = attempts;
        code.ExpiresAt = DateTime.UtcNow.AddMinutes(expiryMinutes);
        await repository.SaveAsync(code);

        // When / Then
        Assert.False(await repository.TryVerifyAsync(code.Id, 5));
    }

    [Fact]
    public async Task TryVerifyAsync_ValidPendingCode_VerifiesOnlyOnceWithoutResettingAttempts()
    {
        // Given
        await using var database = await RelationalTestDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var repository = new EmailVerificationCodeRepository(context);
        var code = await repository.SaveAsync(CreateCode());

        // When / Then
        Assert.True(await repository.TryVerifyAsync(code.Id, 5));
        Assert.False(await repository.TryVerifyAsync(code.Id, 5));
        await using var readContext = database.CreateContext();
        var stored = await readContext.EmailVerificationCodes.SingleAsync();
        Assert.Equal(EmailVerificationCodeStatus.Verified, stored.Status);
        Assert.Equal(4, stored.AttemptCount);
        Assert.NotNull(stored.VerifiedAt);
    }

    private static EmailVerificationCode CreateCode() => new()
    {
        Email = "review@example.invalid",
        CodeHash = Guid.NewGuid().ToString("N"),
        AttemptCount = 4,
        ExpiresAt = DateTime.UtcNow.AddMinutes(5)
    };
}
