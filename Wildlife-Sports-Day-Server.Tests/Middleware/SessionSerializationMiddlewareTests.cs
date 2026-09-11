using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Session;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Wildlife_Sports_Day_Server.Dtos.Requests;
using Wildlife_Sports_Day_Server.Middleware;
using Wildlife_Sports_Day_Server.Services;
using Xunit;

namespace Wildlife_Sports_Day_Server.Tests.Middleware;

public class SessionSerializationMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_ConcurrentScoreRequests_PreservesMaximumAndVerificationState()
    {
        // Given: 실제 DistributedSession 두 개가 같은 저장소와 세션 키를 사용합니다.
        var cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        var sessionKey = Guid.NewGuid().ToString("N");
        var initialSession = CreateSession(cache, sessionKey);
        initialSession.SetInt32("GuestBestScore", 100);
        await initialSession.CommitAsync();
        var firstLoaded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var enteredRequests = 0;
        var service = new GuestService(NullLogger<GuestService>.Instance);
        var middleware = new SessionSerializationMiddleware(async context =>
        {
            Interlocked.Increment(ref enteredRequests);
            context.Session = CreateSession(cache, sessionKey);
            await context.Session.LoadAsync();
            var score = (int)context.Items["score"]!;
            if (score == 200)
            {
                firstLoaded.SetResult();
                await releaseFirst.Task;
                context.Session.SetString("EmailVerification.VerifiedEmail", "verified@example.invalid");
            }

            await service.SaveGuestScoreAsync(new SaveGuestScoreRequest { Score = score }, context);
            // SessionMiddleware가 요청 종료 시 실행하는 저장까지 잠금 안에 포함합니다.
            await context.Session.CommitAsync();
        }, CreateOptions());
        var first = CreateContext(sessionKey);
        first.Items["score"] = 200;
        var second = CreateContext(sessionKey);
        second.Items["score"] = 150;

        // When
        var firstRequest = middleware.InvokeAsync(first);
        await firstLoaded.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var secondRequest = middleware.InvokeAsync(second);
        var enteredBeforeRelease = Volatile.Read(ref enteredRequests);
        releaseFirst.SetResult();
        await Task.WhenAll(firstRequest, secondRequest).WaitAsync(TimeSpan.FromSeconds(5));

        // Then
        Assert.Equal(1, enteredBeforeRelease);
        var stored = CreateSession(cache, sessionKey);
        await stored.LoadAsync();
        Assert.Equal(200, stored.GetInt32("GuestBestScore"));
        Assert.Equal("verified@example.invalid", stored.GetString("EmailVerification.VerifiedEmail"));
    }

    [Fact]
    public async Task InvokeAsync_RequestFails_ReleasesSessionForNextRequest()
    {
        // Given
        var requestCount = 0;
        var middleware = new SessionSerializationMiddleware(_ =>
            Interlocked.Increment(ref requestCount) == 1
                ? Task.FromException(new IOException("Simulate request failure"))
                : Task.CompletedTask, CreateOptions());
        var key = Guid.NewGuid().ToString("N");

        // When / Then
        await Assert.ThrowsAsync<IOException>(() => middleware.InvokeAsync(CreateContext(key)));
        await middleware.InvokeAsync(CreateContext(key)).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(2, requestCount);
    }

    private static IOptions<SessionOptions> CreateOptions()
    {
        var options = new SessionOptions();
        options.Cookie.Name = ".Wildlife.Session";
        return Options.Create(options);
    }

    private static DefaultHttpContext CreateContext(string key)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = $".Wildlife.Session={key}";
        return context;
    }

    private static DistributedSession CreateSession(MemoryDistributedCache cache, string key) =>
        new(cache, key, TimeSpan.FromMinutes(30), TimeSpan.FromSeconds(5),
            () => true, NullLoggerFactory.Instance, isNewSessionKey: false);
}
