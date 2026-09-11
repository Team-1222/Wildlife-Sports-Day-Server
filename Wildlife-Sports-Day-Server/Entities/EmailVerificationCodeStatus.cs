namespace Wildlife_Sports_Day_Server.Entities;

public enum EmailVerificationCodeStatus
{
    Pending,
    Verified,
    Consumed,
    Revoked,
    Expired,
    SendFailed,
    AttemptLimitExceeded
}
