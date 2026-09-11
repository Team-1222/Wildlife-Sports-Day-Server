using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Wildlife_Sports_Day_Server.Dtos.Requests;
using Wildlife_Sports_Day_Server.Services;
using Xunit;

namespace Wildlife_Sports_Day_Server.Tests.Services;

public class GuestServiceTests
{
    [Fact]
    public async Task SaveGuestScoreAsync_NewScore_StoresBestScoreInSession()
    {
        var httpContext = CreateHttpContext();
        var service = new GuestService(NullLogger<GuestService>.Instance);

        var response = await service.SaveGuestScoreAsync(new SaveGuestScoreRequest { Score = 100 }, httpContext);

        Assert.Equal(100, response.BestScore);
        Assert.Equal(100, httpContext.Session.GetInt32("GuestBestScore"));
    }

    [Fact]
    public async Task SaveGuestScoreAsync_LowerScore_KeepsBestScoreInSession()
    {
        var httpContext = CreateHttpContext();
        httpContext.Session.SetInt32("GuestBestScore", 150);
        var service = new GuestService(NullLogger<GuestService>.Instance);

        var response = await service.SaveGuestScoreAsync(new SaveGuestScoreRequest { Score = 100 }, httpContext);

        Assert.Equal(150, response.BestScore);
        Assert.Equal(150, httpContext.Session.GetInt32("GuestBestScore"));
    }

    [Fact]
    public async Task SaveGuestScoreAsync_HigherScore_ReplacesBestScoreInSession()
    {
        var httpContext = CreateHttpContext();
        httpContext.Session.SetInt32("GuestBestScore", 100);
        var service = new GuestService(NullLogger<GuestService>.Instance);

        var response = await service.SaveGuestScoreAsync(new SaveGuestScoreRequest { Score = 150 }, httpContext);

        Assert.Equal(150, response.BestScore);
        Assert.Equal(150, httpContext.Session.GetInt32("GuestBestScore"));
    }

    private static DefaultHttpContext CreateHttpContext()
    {
        var httpContext = new DefaultHttpContext
        {
            Session = new TestSession()
        };

        return httpContext;
    }

    private sealed class TestSession : ISession
    {
        private readonly Dictionary<string, byte[]> values = new();

        public bool IsAvailable => true;
        public string Id => "test-session";
        public IEnumerable<string> Keys => values.Keys;

        public Task LoadAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task CommitAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public bool TryGetValue(string key, out byte[] value) =>
            values.TryGetValue(key, out value!);

        public void Set(string key, byte[] value) =>
            values[key] = value;

        public void Remove(string key) =>
            values.Remove(key);

        public void Clear() =>
            values.Clear();
    }
}
