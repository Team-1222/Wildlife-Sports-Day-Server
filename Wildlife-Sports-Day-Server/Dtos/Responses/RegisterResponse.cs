namespace Wildlife_Sports_Day_Server.Dtos.Responses;

public sealed class RegisterResponse
{
    public required string UserId { get; init; }
    public required string UserName { get; init; }
    public required string Email { get; init; }
    public required string Role { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
}
