using Wildlife_Sports_Day_Server.Dtos.Requests;
using Wildlife_Sports_Day_Server.Dtos.Responses;

namespace Wildlife_Sports_Day_Server.Services;

public class GuestService(ILogger<GuestService> logger) : IGuestService
{
    public async Task<GuestScoreResponse> SaveGuestScoreAsync(SaveGuestScoreRequest request, HttpContext httpContext)
    {
        await httpContext.Session.LoadAsync();

        var currentBestScore = httpContext.Session.GetInt32(GuestSessionKeys.BestScore);
        var bestScore = currentBestScore is null || request.Score > currentBestScore.Value
            ? request.Score
            : currentBestScore.Value;

        httpContext.Session.SetInt32(GuestSessionKeys.BestScore, bestScore);
        await httpContext.Session.CommitAsync();

        logger.LogInformation("Saved guest score in session");

        return new GuestScoreResponse { BestScore = bestScore };
    }
}
