using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;
using Wildlife_Sports_Day_Server.Infrastructure.HealthChecks;
using Wildlife_Sports_Day_Server.Repositories;
using Xunit;

namespace Wildlife_Sports_Day_Server.Tests.Infrastructure.HealthChecks;

public class DatabaseHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_DatabaseReady_ReturnsHealthy()
    {
        var repository = new Mock<IDatabaseHealthRepository>();
        repository
            .Setup(item => item.CanConnectAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        repository
            .Setup(item => item.HasPendingMigrationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        await using var serviceProvider = CreateServiceProvider(repository);
        var healthCheck = CreateHealthCheck(serviceProvider);

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_DatabaseUnavailable_ReturnsUnhealthyWithoutCheckingMigrations()
    {
        var repository = new Mock<IDatabaseHealthRepository>();
        repository
            .Setup(item => item.CanConnectAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        await using var serviceProvider = CreateServiceProvider(repository);
        var healthCheck = CreateHealthCheck(serviceProvider);

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        repository.Verify(
            item => item.HasPendingMigrationsAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CheckHealthAsync_PendingMigrations_ReturnsUnhealthy()
    {
        var repository = new Mock<IDatabaseHealthRepository>();
        repository
            .Setup(item => item.CanConnectAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        repository
            .Setup(item => item.HasPendingMigrationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        await using var serviceProvider = CreateServiceProvider(repository);
        var healthCheck = CreateHealthCheck(serviceProvider);

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_RepositoryThrows_ReturnsUnhealthy()
    {
        var repository = new Mock<IDatabaseHealthRepository>();
        repository
            .Setup(item => item.CanConnectAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database probe failed."));
        await using var serviceProvider = CreateServiceProvider(repository);
        var healthCheck = CreateHealthCheck(serviceProvider);

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.IsType<InvalidOperationException>(result.Exception);
    }

    private static ServiceProvider CreateServiceProvider(Mock<IDatabaseHealthRepository> repository) =>
        new ServiceCollection()
            .AddScoped(_ => repository.Object)
            .BuildServiceProvider();

    private static DatabaseHealthCheck CreateHealthCheck(IServiceProvider serviceProvider) =>
        new(serviceProvider.GetRequiredService<IServiceScopeFactory>());
}
