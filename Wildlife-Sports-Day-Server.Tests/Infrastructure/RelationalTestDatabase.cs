using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Wildlife_Sports_Day_Server.Infrastructure;

namespace Wildlife_Sports_Day_Server.Tests.Infrastructure;

// 실제 PostgreSQL의 잠금 검증과 별개로 조건부 갱신과 트랜잭션을 실행하는 격리된 관계형 DB입니다.
internal sealed class RelationalTestDatabase : IAsyncDisposable
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");

    public static async Task<RelationalTestDatabase> CreateAsync()
    {
        var database = new RelationalTestDatabase();
        await database.connection.OpenAsync();
        database.connection.CreateFunction("NOW", () => DateTime.UtcNow);
        await using var context = database.CreateContext();
        await context.Database.EnsureCreatedAsync();
        return database;
    }

    public AppDbContext CreateContext(params IInterceptor[] interceptors) => new(
        new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(interceptors)
            .Options);

    public async ValueTask DisposeAsync() => await connection.DisposeAsync();
}
