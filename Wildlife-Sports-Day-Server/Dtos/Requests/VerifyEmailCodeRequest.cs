using System.ComponentModel.DataAnnotations;

namespace Wildlife_Sports_Day_Server.Dtos.Requests;

public class VerifyEmailCodeRequest
{
    [Required(ErrorMessage = "이메일은 필수입니다.")]
    [EmailAddress(ErrorMessage = "올바른 이메일 형식이 아닙니다.")]
    public string Email { get; init; } = null!;

    [Required(ErrorMessage = "인증 코드는 필수입니다.")]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "인증 코드는 6자리여야 합니다.")]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "인증 코드는 숫자 6자리여야 합니다.")]
    public string Code { get; init; } = null!;
}
