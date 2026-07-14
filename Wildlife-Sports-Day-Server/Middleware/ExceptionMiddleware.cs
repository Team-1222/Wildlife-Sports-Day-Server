using Wildlife_Sports_Day_Server.Dtos.Responses;
using Wildlife_Sports_Day_Server.Exceptions;

namespace Wildlife_Sports_Day_Server.Middleware;

public class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)//HttpContext = 요청과 응답에 대한 모든 정보
                                                      //(주소, 메서드, 헤더, 쿠키, 세션, 사용자 정보, 응답 상태)
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
        var response = new ApiResponse<object>
        {
            Success = false,
            Message = message,
            Code = "ERROR"
        };
        await context.Response.WriteAsJsonAsync(response);
    }
}
