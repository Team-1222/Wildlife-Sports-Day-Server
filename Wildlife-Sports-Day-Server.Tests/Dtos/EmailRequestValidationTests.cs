using System.ComponentModel.DataAnnotations;
using Wildlife_Sports_Day_Server.Dtos.Requests;
using Xunit;

namespace Wildlife_Sports_Day_Server.Tests.Dtos;

public class EmailRequestValidationTests
{
    [Theory]
    [InlineData("send", 255, true)]
    [InlineData("send", 256, false)]
    [InlineData("send", 276, false)]
    [InlineData("verify", 255, true)]
    [InlineData("verify", 256, false)]
    [InlineData("verify", 276, false)]
    [InlineData("register", 255, true)]
    [InlineData("register", 256, false)]
    [InlineData("register", 276, false)]
    public void ValidateEmail_DatabaseLengthBoundary_RejectsOversizedInput(string kind, int length, bool expected)
    {
        // Given
        const string domain = "@example.com";
        var email = new string('a', length - domain.Length) + domain;
        object request = kind switch
        {
            "send" => new SendVerificationCodeRequest { Email = email },
            "verify" => new VerifyEmailCodeRequest { Email = email },
            _ => new RegisterRequest { Email = email }
        };
        var results = new List<ValidationResult>();

        // When
        var valid = Validator.TryValidateProperty(email,
            new ValidationContext(request) { MemberName = "Email" }, results);

        // Then
        Assert.Equal(expected, valid);
        if (!expected)
        {
            Assert.Contains(results, result => result.ErrorMessage == "이메일은 255자 이내로 입력하십시오.");
        }
    }
}
