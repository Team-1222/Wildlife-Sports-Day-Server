using Microsoft.EntityFrameworkCore;
using Npgsql;
using Wildlife_Sports_Day_Server.Infrastructure;
using Xunit;

namespace Wildlife_Sports_Day_Server.Tests.Infrastructure;

[CollectionDefinition("Design-time environment", DisableParallelization = true)]
public class DesignTimeEnvironmentCollection;

[Collection("Design-time environment")]
public sealed class AppDbContextFactoryTests : IDisposable
{
    private static readonly string[] VariableNames =
        ["DOTNET_ENVIRONMENT", "ASPNETCORE_ENVIRONMENT", "ConnectionStrings__DefaultConnection"];
    private readonly Dictionary<string, string?> previousValues = VariableNames
        .ToDictionary(name => name, Environment.GetEnvironmentVariable);

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public void CreateDbContext_EnvironmentConnection_UsesConfiguredConnection(string environment)
    {
        // Given: 연결을 열지 않으며 환경 변수로 전달하는 테스트용 설정입니다.
        var connection = new NpgsqlConnectionStringBuilder { Host = "localhost", Database = "factory_test" }.ToString();
        Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", environment);
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", connection);

        // When
        using var context = new AppDbContextFactory().CreateDbContext([]);

        // Then
        Assert.Equal(connection, context.Database.GetConnectionString());
    }

    [Fact]
    public void CreateDbContext_NoConnection_AllowsOfflineModelOperations()
    {
        // Given
        Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Production");
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", null);

        // When
        using var context = new AppDbContextFactory().CreateDbContext([]);

        // Then
        Assert.Null(context.Database.GetConnectionString());
        Assert.False(context.Database.HasPendingModelChanges());
    }

    public void Dispose()
    {
        foreach (var (name, value) in previousValues)
        {
            Environment.SetEnvironmentVariable(name, value);
        }
    }
}
