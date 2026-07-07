using System.ComponentModel.DataAnnotations;

namespace Wildlife_Sports_Day_Server.Dtos.Requests;

public class SaveGuestScoreRequest
{
    [Range(0, int.MaxValue, ErrorMessage = "점수는 0 이상이어야 합니다.")]
    public int Score { get; init; }
}
