namespace Wildlife_Sports_Day_Server.Dtos.Responses;

public sealed class ApiResponse<T>
{
    public required bool Success { get; init; }
    public required string Message { get; init; }
    public string? Code { get; init; }
    public T? Data { get; init; }
}
