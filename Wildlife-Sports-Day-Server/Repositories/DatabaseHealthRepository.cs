using Microsoft.EntityFrameworkCore;
using Wildlife_Sports_Day_Server.Infrastructure;

namespace Wildlife_Sports_Day_Server.Repositories;

public sealed class DatabaseHealthRepository(AppDbContext dbContext) : IDatabaseHealthRepository
{
    public Task<bool> CanConnectAsync(CancellationToken cancellationToken = default) =>
        dbContext.Database.CanConnectAsync(cancellationToken);

    public async Task<bool> HasPendingMigrationsAsync(CancellationToken cancellationToken = default) =>
        (await dbContext.Database.GetPendingMigrationsAsync(cancellationToken)).Any();
}
