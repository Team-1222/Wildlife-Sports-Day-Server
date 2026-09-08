using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Wildlife_Sports_Day_Server.Entities;
using Wildlife_Sports_Day_Server.Repositories;
using Wildlife_Sports_Day_Server.Tests.Infrastructure;
using Xunit;

namespace Wildlife_Sports_Day_Server.Tests.Repositories;

public class UserRepositoryTests
{
    [Fact]
    public async Task SaveWithVerificationAsync_ValidCode_CommitsUserAndCodeTogether()
    {
        // Given
        await using var database = await RelationalTestDatabase.CreateAsync();
        var code = await SeedCodeAsync(database);
        await using var context = database.CreateContext();
        var user = CreateUser();

        // When
        var result = await new UserRepository(context)
            .SaveWithVerificationAsync(user, code.Id, DateTime.UtcNow.AddMinutes(-5));

        // Then
        Assert.Equal(UserRegistrationResult.Saved, result);
        await using var readContext = database.CreateContext();
        Assert.Equal(user.Id, (await readContext.Users.SingleAsync()).Id);
        var storedCode = await readContext.EmailVerificationCodes.SingleAsync();
        Assert.Equal(EmailVerificationCodeStatus.Consumed, storedCode.Status);
        Assert.NotNull(storedCode.UnavailableAt);
    }

    [Fact]
    public async Task SaveWithVerificationAsync_FailureAfterInsert_RollsBackUserAndCode()
    {
        // Given: INSERT가 실행된 다음, 트랜잭션 커밋 전에 실패합니다.
        await using var database = await RelationalTestDatabase.CreateAsync();
        var code = await SeedCodeAsync(database);
        var failure = new FailAfterInsertInterceptor();
        await using var context = database.CreateContext(failure);

        // When
        await Assert.ThrowsAsync<IOException>(() => new UserRepository(context)
            .SaveWithVerificationAsync(CreateUser(), code.Id, DateTime.UtcNow.AddMinutes(-5)));

        // Then
        Assert.True(failure.InsertObserved);
        await using var readContext = database.CreateContext();
        Assert.Empty(await readContext.Users.ToListAsync());
        var storedCode = await readContext.EmailVerificationCodes.SingleAsync();
        Assert.Equal(EmailVerificationCodeStatus.Verified, storedCode.Status);
        Assert.Null(storedCode.UnavailableAt);
    }

    [Theory]
    [InlineData(EmailVerificationCodeStatus.Revoked, 0)]
    [InlineData(EmailVerificationCodeStatus.Consumed, 0)]
    [InlineData(EmailVerificationCodeStatus.Verified, -6)]
    public async Task SaveWithVerificationAsync_UnavailableCode_DoesNotInsertUser(
        EmailVerificationCodeStatus status, int verifiedMinutesAgo)
    {
        // Given
        await using var database = await RelationalTestDatabase.CreateAsync();
        var code = await SeedCodeAsync(database, status, verifiedMinutesAgo);
        await using var context = database.CreateContext();

        // When
        var result = await new UserRepository(context)
            .SaveWithVerificationAsync(CreateUser(), code.Id, DateTime.UtcNow.AddMinutes(-5));

        // Then
        Assert.Equal(UserRegistrationResult.VerificationUnavailable, result);
        Assert.Empty(await context.Users.ToListAsync());
    }

    private static async Task<EmailVerificationCode> SeedCodeAsync(RelationalTestDatabase database,
        EmailVerificationCodeStatus status = EmailVerificationCodeStatus.Verified, int verifiedMinutesAgo = 0)
    {
        await using var context = database.CreateContext();
        var code = new EmailVerificationCode
        {
            Email = "registration@example.invalid",
            CodeHash = Guid.NewGuid().ToString("N"),
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            Status = status,
            VerifiedAt = DateTime.UtcNow.AddMinutes(verifiedMinutesAgo)
        };
        context.EmailVerificationCodes.Add(code);
        await context.SaveChangesAsync();
        return code;
    }

    private static User CreateUser() => new()
    {
        Email = "registration@example.invalid",
        Nickname = "registration-test",
        PasswordHash = Guid.NewGuid().ToString("N")
    };

    private sealed class FailAfterInsertInterceptor : SaveChangesInterceptor
    {
        public bool InsertObserved { get; private set; }

        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default)
        {
            InsertObserved = eventData.Context!.ChangeTracker.Entries<User>().Any(entry => entry.Entity.Id > 0);
            throw new IOException("Simulate failure after inserting user");
        }
    }
}
