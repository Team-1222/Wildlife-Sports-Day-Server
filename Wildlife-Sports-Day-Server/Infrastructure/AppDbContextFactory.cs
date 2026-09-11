using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Wildlife_Sports_Day_Server.Infrastructure;

public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environments.Development;
        var configurationBuilder = new ConfigurationBuilder();
        if (string.Equals(environment, Environments.Development, StringComparison.OrdinalIgnoreCase))
        {
            configurationBuilder.AddUserSecrets<AppDbContextFactory>(optional: true);
        }

        var configuration = configurationBuilder.AddEnvironmentVariables().Build();
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        // 연결 정보가 없는 이미지 빌드에서도 모델·bundle 생성은 가능해야 합니다.
        // 실제 bundle 실행의 --connection 인수는 EF 도구가 이 설정보다 우선 적용합니다.
        optionsBuilder.UseNpgsql(configuration.GetConnectionString("DefaultConnection"));
        return new AppDbContext(optionsBuilder.Options);
    }
}
