using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wildlife_Sports_Day_Server.Dtos.Requests;
using Wildlife_Sports_Day_Server.Dtos.Responses;
using Wildlife_Sports_Day_Server.Services;

namespace Wildlife_Sports_Day_Server.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/guest")]
public class GuestController(IGuestService guestService) : ControllerBase
{
    [HttpPost("scores")]
    public async Task<ActionResult<ApiResponse<GuestScoreResponse>>> SaveGuestScoreAsync(
        [FromBody] SaveGuestScoreRequest request)
    {
        var result = await guestService.SaveGuestScoreAsync(request, HttpContext);
        return Ok(new ApiResponse<GuestScoreResponse>
        {
            Success = true,
            Message = "게스트 점수가 임시 저장되었습니다.",
            Data = result
        });
    }
}
