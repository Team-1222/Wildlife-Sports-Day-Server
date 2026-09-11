namespace Wildlife_Sports_Day_Server.Services;

public sealed class LoginAttemptTracker : ILoginAttemptTracker, IDisposable
{
    private const int MaxAttempts = 5;
    private static readonly TimeSpan AttemptWindow = TimeSpan.FromMinutes(5);
    private readonly object sync = new();
    private readonly Dictionary<string, AttemptState> attempts = new(StringComparer.Ordinal);
    private readonly TimeProvider timeProvider;
    private readonly int capacity;
    private readonly ITimer cleanupTimer;

    public LoginAttemptTracker(TimeProvider timeProvider, int capacity = 10_000)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        this.timeProvider = timeProvider;
        this.capacity = capacity;
        cleanupTimer = timeProvider.CreateTimer(_ => RemoveExpired(), null,
            TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }

    public int TrackedKeyCount
    {
        get { lock (sync) { return attempts.Count; } }
    }

    public bool IsBlocked(string key)
    {
        lock (sync)
        {
            var now = timeProvider.GetUtcNow();
            if (attempts.TryGetValue(key, out var state))
            {
                if (state.StartedAtUtc + AttemptWindow > now)
                {
                    return state.Count >= MaxAttempts;
                }

                attempts.Remove(key);
            }

            if (attempts.Count >= capacity)
            {
                RemoveExpiredCore(now);
                if (attempts.Count >= capacity)
                {
                    return true;
                }
            }

            // 조회 전에 자리를 확보해 서로 다른 키의 동시 요청도 용량을 초과하지 않습니다.
            attempts[key] = new AttemptState(0, now);
            return false;
        }
    }

    public void RecordFailure(string key)
    {
        lock (sync)
        {
            var now = timeProvider.GetUtcNow();
            if (attempts.TryGetValue(key, out var state) && state.StartedAtUtc + AttemptWindow > now)
            {
                attempts[key] = state with { Count = state.Count + 1 };
            }
            else if (attempts.Count < capacity || attempts.ContainsKey(key))
            {
                attempts[key] = new AttemptState(1, now);
            }
        }
    }

    public void Reset(string key)
    {
        lock (sync) { attempts.Remove(key); }
    }

    private void RemoveExpired()
    {
        lock (sync) { RemoveExpiredCore(timeProvider.GetUtcNow()); }
    }

    private void RemoveExpiredCore(DateTimeOffset now)
    {
        var expiredKeys = attempts.Where(pair => pair.Value.StartedAtUtc + AttemptWindow <= now)
            .Select(pair => pair.Key).ToArray();
        foreach (var key in expiredKeys)
        {
            attempts.Remove(key);
        }
    }

    public void Dispose() => cleanupTimer.Dispose();

    private sealed record AttemptState(int Count, DateTimeOffset StartedAtUtc);
}
