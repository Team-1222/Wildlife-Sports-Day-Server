using Microsoft.EntityFrameworkCore;
using Wildlife_Sports_Day_Server.Entities;
using Wildlife_Sports_Day_Server.Infrastructure;

namespace Wildlife_Sports_Day_Server.Repositories;

public class EmailVerificationCodeRepository(AppDbContext dbContext) : IEmailVerificationCodeRepository
{
    public async Task<EmailVerificationCode?> FindLatestByEmailAsync(string email) =>
        await dbContext.EmailVerificationCodes
            .Where(code => code.Email == email)
            .OrderByDescending(code => code.CreatedAt)
            .ThenByDescending(code => code.Id)
            .FirstOrDefaultAsync();

    public async Task<EmailVerificationCode> SaveAsync(EmailVerificationCode verificationCode)
    {
        dbContext.EmailVerificationCodes.Add(verificationCode);
        await dbContext.SaveChangesAsync();
        return verificationCode;
    }

    public async Task UpdateAsync(EmailVerificationCode verificationCode)
    {
        dbContext.EmailVerificationCodes.Update(verificationCode);
        await dbContext.SaveChangesAsync();
    }

    public async Task InvalidateAllByEmailAsync(string email)
    {
        var codes = await dbContext.EmailVerificationCodes
            .Where(code => code.Email == email && !code.IsUsed)
            .ToListAsync();

        foreach (var code in codes)
        {
            code.IsUsed = true;
            code.UsedAt = DateTime.UtcNow;
        }

        await dbContext.SaveChangesAsync();
    }
}
