using Microsoft.Extensions.Options;
using Wildlife_Sports_Day_Server.Infrastructure.Concurrency;

namespace Wildlife_Sports_Day_Server.Middleware;

// 현재 단일 프로세스 메모리 세션의 읽기부터 SessionMiddleware의 최종 저장까지 보호합니다.
public sealed class SessionSerializationMiddleware(RequestDelegate next, IOptions<SessionOptions> options)
{
    private static readonly StripedAsyncLock SessionLocks = new();

    public async Task InvokeAsync(HttpContext context)
    {
        var sessionCookie = context.Request.Cookies[options.Value.Cookie.Name!];
        if (string.IsNullOrEmpty(sessionCookie))
        {
            await next(context);
            return;
        }

        using var sessionLock = await SessionLocks.AcquireAsync(sessionCookie, context.RequestAborted);
        await next(context);
    }
}
