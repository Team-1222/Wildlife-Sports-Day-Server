using System.Text;

namespace Wildlife_Sports_Day_Server.Infrastructure.Security;

public static class PasswordPolicy
{
    public const int MaximumUtf8Bytes = 72;
    public const string TooLongMessage = "비밀번호는 UTF-8 기준 72바이트 이내로 입력하십시오.";

    public static bool IsWithinByteLimit(string value) =>
        value.Length <= MaximumUtf8Bytes && Encoding.UTF8.GetByteCount(value) <= MaximumUtf8Bytes;
}
