using Microsoft.AspNetCore.Mvc;
using Wildlife_Sports_Day_Server.Dtos.Requests;
using Wildlife_Sports_Day_Server.Dtos.Responses;
using Wildlife_Sports_Day_Server.Services;

namespace Wildlife_Sports_Day_Server.Controllers;

[ApiController]
[Route("Auth")]
public class AuthController(IAuthService authService) : ControllerBase
{
    [HttpPost("Email-Code/Send")]
    public async Task<ActionResult<ApiResponse<MessageResponse>>> SendVerificationCode(
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

    [HttpPost("Email-Code/Verify")]
    public async Task<ActionResult<ApiResponse<MessageResponse>>> VerifyEmailCode(
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

    [HttpPost("Register")]
    public async Task<ActionResult<ApiResponse<RegisterResponse>>> Register([FromBody] RegisterRequest request)
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
