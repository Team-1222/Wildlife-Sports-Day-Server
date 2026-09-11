using System.ComponentModel.DataAnnotations;
using Wildlife_Sports_Day_Server.Dtos.Requests;
using Xunit;

namespace Wildlife_Sports_Day_Server.Tests.Dtos;

public class PasswordRequestValidationTests
{
    public static IEnumerable<object[]> PasswordBoundaries()
    {
        var prefix = string.Concat("A", "a", "1", "!");
        var inputs = new (string Value, bool Accepted)[]
        {
            (prefix + new string('x', 67), true),
            (prefix + new string('x', 68), true),
            (prefix + new string('x', 69), false),
            (prefix + new string('한', 22) + "xx", true),
            (prefix + new string('한', 23), false),
            (prefix + string.Concat(Enumerable.Repeat("😀", 17)), true),
            (prefix + string.Concat(Enumerable.Repeat("😀", 17)) + "x", false)
        };
        foreach (var kind in new[] { "register", "confirm", "login" })
        {
            foreach (var (value, accepted) in inputs)
            {
                yield return new object[] { kind, value, accepted };
            }
        }
    }

    [Theory]
    [MemberData(nameof(PasswordBoundaries))]
    public void ValidatePassword_Utf8ByteBoundary_AcceptsOnlyWithinLimit(string kind, string credential, bool expected)
    {
        // Given
        object request = kind == "login" ? new LoginRequest() : new RegisterRequest();
        var property = kind == "confirm" ? nameof(RegisterRequest.ConfirmPassword) : nameof(RegisterRequest.Password);
        var results = new List<ValidationResult>();

        // When
        var valid = Validator.TryValidateProperty(credential,
            new ValidationContext(request) { MemberName = property }, results);

        // Then
        Assert.Equal(expected, valid);
        if (!expected)
        {
            Assert.Contains(results, result => result.ErrorMessage == "비밀번호는 UTF-8 기준 72바이트 이내로 입력하십시오.");
        }
    }
}
