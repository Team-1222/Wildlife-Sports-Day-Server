namespace Wildlife_Sports_Day_Server.Repositories;

public interface IDatabaseHealthRepository
{
    Task<bool> CanConnectAsync(CancellationToken cancellationToken = default);
    Task<bool> HasPendingMigrationsAsync(CancellationToken cancellationToken = default);
}
