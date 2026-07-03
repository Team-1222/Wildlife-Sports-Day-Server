using Wildlife_Sports_Day_Server.Dtos.Responses;
using Wildlife_Sports_Day_Server.Exceptions;

namespace Wildlife_Sports_Day_Server.Middleware;

public class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (AppException exception)
        {
            logger.LogWarning("Handled application exception: {Message}", exception.Message);
            await WriteErrorResponseAsync(context, exception.StatusCode, exception.Message);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unhandled exception occurred");
            await WriteErrorResponseAsync(
                context,
                StatusCodes.Status500InternalServerError,
                "서버 내부 오류가 발생했습니다.");
        }
    }

    private static async Task WriteErrorResponseAsync(HttpContext context, int statusCode, string message)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        var response = ApiResponse<object>.Fail(message);
        await context.Response.WriteAsJsonAsync(response);
    }
}
