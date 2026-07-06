using Microsoft.AspNetCore.Mvc;
using Wildlife_Sports_Day_Server.Dtos.Requests;
using Wildlife_Sports_Day_Server.Dtos.Responses;
using Wildlife_Sports_Day_Server.Services;

namespace Wildlife_Sports_Day_Server.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IAuthService authService) : ControllerBase
{
    [HttpPost("email-code/send")]
    public async Task<ActionResult<ApiResponse<MessageResponse>>> SendVerificationCodeAsync(
        [FromBody] SendVerificationCodeRequest request)
    {
        var result = await authService.SendVerificationEmailAsync(request);
        return Ok(new ApiResponse<MessageResponse>
        {
            Success = true,
            Message = result.Message,
            Data = result
        });
    }

    [HttpPost("email-code/verify")]
    public async Task<ActionResult<ApiResponse<MessageResponse>>> VerifyEmailCodeAsync(
        [FromBody] VerifyEmailCodeRequest request)
    {
        var result = await authService.VerifyEmailCodeAsync(request);
        return Ok(new ApiResponse<MessageResponse>
        {
            Success = true,
            Message = result.Message,
            Data = result
        });
    }

    [HttpPost("register")]
    public async Task<ActionResult<ApiResponse<RegisterResponse>>> RegisterAsync([FromBody] RegisterRequest request)
    {
        var result = await authService.RegisterAsync(request);
        return Ok(new ApiResponse<RegisterResponse>
        {
            Success = true,
            Message = "회원가입이 완료되었습니다.",
            Data = result
        });
    }
}
