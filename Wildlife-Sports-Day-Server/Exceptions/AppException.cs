namespace Wildlife_Sports_Day_Server.Exceptions;

public class AppException(string message, int statusCode = StatusCodes.Status400BadRequest)
    : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
