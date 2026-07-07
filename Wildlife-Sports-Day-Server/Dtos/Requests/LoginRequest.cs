using System.ComponentModel.DataAnnotations;

namespace Wildlife_Sports_Day_Server.Dtos.Requests;

public class LoginRequest
{
    [Required(ErrorMessage = "닉네임은 필수입니다.")]
    [StringLength(20, MinimumLength = 2, ErrorMessage = "닉네임은 2~20자여야 합니다.")]
    public string Nickname { get; init; } = null!;

    [Required(ErrorMessage = "비밀번호는 필수입니다.")]
    public string Password { get; init; } = null!;
}
