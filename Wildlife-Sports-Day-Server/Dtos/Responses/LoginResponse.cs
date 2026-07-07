namespace Wildlife_Sports_Day_Server.Dtos.Responses;

public sealed class LoginResponse
{
    public required int UserId { get; init; }
    public required string Username { get; init; }
    public required string Email { get; init; }
    public required string Role { get; init; }
}
