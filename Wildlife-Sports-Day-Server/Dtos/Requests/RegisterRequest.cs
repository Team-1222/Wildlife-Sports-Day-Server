using System.ComponentModel.DataAnnotations;
using Wildlife_Sports_Day_Server.Infrastructure.Security;
using Wildlife_Sports_Day_Server.Infrastructure.Validation;

namespace Wildlife_Sports_Day_Server.Dtos.Requests;

public class RegisterRequest
{
    [Required(ErrorMessage = "이메일은 필수입니다.")]
    [EmailAddress(ErrorMessage = "올바른 이메일 형식이 아닙니다.")]
    [StringLength(255, ErrorMessage = "이메일은 255자 이내로 입력하십시오.")]
    public string Email { get; init; } = null!;

    [Required(ErrorMessage = "닉네임은 필수입니다.")]
    [StringLength(20, MinimumLength = 2, ErrorMessage = "닉네임은 2~20자여야 합니다.")]
    public string Nickname { get; init; } = null!;

    [Required(ErrorMessage = "비밀번호는 필수입니다.")]
    [PasswordByteLength]
    [StringLength(PasswordPolicy.MaximumUtf8Bytes, MinimumLength = 8,
        ErrorMessage = "비밀번호는 8자 이상, UTF-8 기준 72바이트 이내로 입력하십시오.")]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[\W_]).+$",
        ErrorMessage = "비밀번호는 대소문자, 숫자, 특수문자를 포함해야 합니다.")]
    public string Password { get; init; } = null!;

    [Required(ErrorMessage = "비밀번호 확인은 필수입니다.")]
    [PasswordByteLength]
    public string ConfirmPassword { get; init; } = null!;
}
