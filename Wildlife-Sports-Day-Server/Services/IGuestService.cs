using Wildlife_Sports_Day_Server.Dtos.Requests;
using Wildlife_Sports_Day_Server.Dtos.Responses;

namespace Wildlife_Sports_Day_Server.Services;

public interface IGuestService
{
    Task<GuestScoreResponse> SaveGuestScoreAsync(SaveGuestScoreRequest request, HttpContext httpContext);
}
