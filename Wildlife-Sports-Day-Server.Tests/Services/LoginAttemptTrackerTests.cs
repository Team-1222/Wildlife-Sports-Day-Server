using Wildlife_Sports_Day_Server.Services;
using Xunit;

namespace Wildlife_Sports_Day_Server.Tests.Services;

public class LoginAttemptTrackerTests
{
    [Fact]
    public void IsBlocked_ExpiredKeysWithoutFurtherRequests_RemovesEntriesOnTimer()
    {
        // Given
        var clock = new ManualTimeProvider();
        using var tracker = new LoginAttemptTracker(clock, 2);
        Assert.False(tracker.IsBlocked("old-key"));
        tracker.RecordFailure("old-key");

        // When: 같은 키를 다시 조회하지 않고 시간만 진행합니다.
        clock.Advance(TimeSpan.FromMinutes(6));

        // Then
        Assert.Equal(0, tracker.TrackedKeyCount);
    }

    [Fact]
    public void IsBlocked_CapacityReached_RejectsNewKeysWithoutEvictingBlockedKeys()
    {
        // Given
        var clock = new ManualTimeProvider();
        using var tracker = new LoginAttemptTracker(clock, 2);
        Assert.False(tracker.IsBlocked("blocked-key"));
        for (var i = 0; i < 5; i++) { tracker.RecordFailure("blocked-key"); }
        Assert.False(tracker.IsBlocked("second-key"));

        // When / Then
        Assert.True(tracker.IsBlocked("new-key"));
        Assert.True(tracker.IsBlocked("blocked-key"));
        Assert.Equal(2, tracker.TrackedKeyCount);
        clock.Advance(TimeSpan.FromMinutes(6));
        Assert.False(tracker.IsBlocked("new-key"));
    }

    [Fact]
    public void Reset_SuccessfulLogin_RemovesFailureRecord()
    {
        // Given
        using var tracker = new LoginAttemptTracker(new ManualTimeProvider());
        Assert.False(tracker.IsBlocked("key"));
        tracker.RecordFailure("key");

        // When
        tracker.Reset("key");

        // Then
        Assert.Equal(0, tracker.TrackedKeyCount);
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset now = DateTimeOffset.UtcNow;
        private ManualTimer? timer;

        public override DateTimeOffset GetUtcNow() => now;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
            timer = new ManualTimer(callback, state);

        public void Advance(TimeSpan elapsed)
        {
            now += elapsed;
            timer?.Tick();
        }

        private sealed class ManualTimer(TimerCallback callback, object? state) : ITimer
        {
            private bool disposed;
            public void Tick() { if (!disposed) { callback(state); } }
            public bool Change(TimeSpan dueTime, TimeSpan period) => !disposed;
            public void Dispose() => disposed = true;
            public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
        }
    }
}
