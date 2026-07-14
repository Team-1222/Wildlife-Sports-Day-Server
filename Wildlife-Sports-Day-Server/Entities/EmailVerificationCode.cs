namespace Wildlife_Sports_Day_Server.Entities;

public class EmailVerificationCode
{
    public int Id { get; set; }
    public string Email { get; set; } = null!;
    public string CodeHash { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
    public int AttemptCount { get; set; }
    public EmailVerificationCodeStatus Status { get; set; } = EmailVerificationCodeStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? VerifiedAt { get; set; }
    public DateTime? UnavailableAt { get; set; }
}
