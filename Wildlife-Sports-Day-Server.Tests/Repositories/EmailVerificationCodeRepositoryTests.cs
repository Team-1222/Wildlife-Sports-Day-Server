using Microsoft.EntityFrameworkCore;
using Wildlife_Sports_Day_Server.Entities;
using Wildlife_Sports_Day_Server.Repositories;
using Wildlife_Sports_Day_Server.Tests.Infrastructure;
using Xunit;

namespace Wildlife_Sports_Day_Server.Tests.Repositories;

public class EmailVerificationCodeRepositoryTests
{
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
