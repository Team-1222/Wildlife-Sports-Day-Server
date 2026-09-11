using System.ComponentModel.DataAnnotations;
using Wildlife_Sports_Day_Server.Infrastructure.Security;

namespace Wildlife_Sports_Day_Server.Infrastructure.Validation;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class PasswordByteLengthAttribute() : ValidationAttribute(PasswordPolicy.TooLongMessage)
{
    public override bool IsValid(object? value) =>
        value is null || value is string password && PasswordPolicy.IsWithinByteLimit(password);
}
