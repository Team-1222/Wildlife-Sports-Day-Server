using Microsoft.Extensions.Diagnostics.HealthChecks;
using Wildlife_Sports_Day_Server.Repositories;

namespace Wildlife_Sports_Day_Server.Infrastructure.HealthChecks;

public sealed class DatabaseHealthCheck(IServiceScopeFactory serviceScopeFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<IDatabaseHealthRepository>();
            var canConnect = await repository.CanConnectAsync(cancellationToken);

            if (!canConnect)
            {
                return HealthCheckResult.Unhealthy("Database connection failed.");
            }

            var hasPendingMigrations = await repository.HasPendingMigrationsAsync(cancellationToken);

            return hasPendingMigrations
                ? HealthCheckResult.Unhealthy("Database has pending migrations.")
                : HealthCheckResult.Healthy("Database is ready.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("Database readiness check failed.", exception);
        }
    }
}
