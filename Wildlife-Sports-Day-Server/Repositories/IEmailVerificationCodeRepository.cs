using Wildlife_Sports_Day_Server.Entities;

namespace Wildlife_Sports_Day_Server.Repositories;

public interface IEmailVerificationCodeRepository
{
    Task<EmailVerificationCode?> FindLatestByEmailAsync(string email);
    Task<EmailVerificationCode?> FindLatestActiveByEmailAsync(string email);
    Task<EmailVerificationCode> SaveAsync(EmailVerificationCode verificationCode);
    Task UpdateAsync(EmailVerificationCode verificationCode);
    Task RevokeUsableByEmailAsync(string email);
    Task RevokeActiveByEmailExceptAsync(string email, int retainedCodeId);
}
