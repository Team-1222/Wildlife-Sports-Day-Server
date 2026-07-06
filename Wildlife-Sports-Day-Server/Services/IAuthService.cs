using Wildlife_Sports_Day_Server.Dtos.Requests;
using Wildlife_Sports_Day_Server.Dtos.Responses;

namespace Wildlife_Sports_Day_Server.Services;

public interface IAuthService
{
    Task<MessageResponse> SendVerificationEmailAsync(SendVerificationCodeRequest request);
    Task<MessageResponse> VerifyEmailCodeAsync(VerifyEmailCodeRequest request);
    Task<RegisterResponse> RegisterAsync(RegisterRequest request);
}
