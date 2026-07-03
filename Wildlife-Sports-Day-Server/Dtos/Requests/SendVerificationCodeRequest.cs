using System.ComponentModel.DataAnnotations;

namespace Wildlife_Sports_Day_Server.Dtos.Requests;

public class SendVerificationCodeRequest
{
    [Required(ErrorMessage = "이메일은 필수입니다.")]
    [EmailAddress(ErrorMessage = "올바른 이메일 형식이 아닙니다.")]
    public string Email { get; init; } = null!;
}
