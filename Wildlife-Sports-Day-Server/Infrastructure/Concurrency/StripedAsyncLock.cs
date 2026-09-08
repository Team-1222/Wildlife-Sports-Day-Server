namespace Wildlife_Sports_Day_Server.Infrastructure.Concurrency;

// 입력 키가 증가해도 잠금 객체 수는 일정하게 유지합니다.
public sealed class StripedAsyncLock
{
    private readonly SemaphoreSlim[] gates = Enumerable.Range(0, 256)
        .Select(_ => new SemaphoreSlim(1, 1)).ToArray();

    public async Task<IDisposable> AcquireAsync(string key, CancellationToken cancellationToken = default)
    {
        var gate = gates[(uint)StringComparer.Ordinal.GetHashCode(key) % (uint)gates.Length];
        await gate.WaitAsync(cancellationToken);
        return new Lease(gate);
    }

    private sealed class Lease(SemaphoreSlim gate) : IDisposable
    {
        private SemaphoreSlim? heldGate = gate;

        public void Dispose() => Interlocked.Exchange(ref heldGate, null)?.Release();
    }
}
