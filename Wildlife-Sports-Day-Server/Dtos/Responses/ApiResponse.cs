namespace Wildlife_Sports_Day_Server.Dtos.Responses;

public class ApiResponse<T>
{
    public bool Success { get; init; }
    public T? Data { get; init; }
    public ErrorDetail? Error { get; init; }

    public static ApiResponse<T> Ok(T data) =>
        new() { Success = true, Data = data };

    public static ApiResponse<T> Fail(string message, string? code = null) =>
        new() { Success = false, Error = new ErrorDetail(code ?? "ERROR", message) };
}

public record ErrorDetail(string Code, string Message);
/*
    public string Code {get; init; } = string.Empty;
    public string Message {get; init; } = string.Empty;
 */
