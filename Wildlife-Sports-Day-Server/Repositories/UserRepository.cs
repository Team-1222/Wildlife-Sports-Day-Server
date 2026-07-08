using Microsoft.EntityFrameworkCore;
using Npgsql;
using Wildlife_Sports_Day_Server.Entities;
using Wildlife_Sports_Day_Server.Infrastructure;

namespace Wildlife_Sports_Day_Server.Repositories;

public class UserRepository(AppDbContext dbContext) : IUserRepository
{
    public async Task<bool> ExistsByEmailAsync(string email) =>
        await dbContext.Users.AnyAsync(user => user.Email == email);

    public async Task<bool> ExistsByNicknameAsync(string nickname) =>
        await dbContext.Users.AnyAsync(user => user.Nickname == nickname);

    public async Task<User?> FindByEmailAsync(string email) =>
        await dbContext.Users.FirstOrDefaultAsync(user => user.Email == email);

    public async Task<User?> FindByNicknameAsync(string nickname) =>
        await dbContext.Users.FirstOrDefaultAsync(user => user.Nickname == nickname);

    public async Task<User> SaveAsync(User user)
    {
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
        return user;
    }

    public async Task<User?> SaveIfUniqueAsync(User user)
    {
        dbContext.Users.Add(user);

        try
        {
            await dbContext.SaveChangesAsync();
            return user;
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            dbContext.Entry(user).State = EntityState.Detached;
            return null;
        }
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.GetBaseException() is PostgresException postgresException
        && postgresException.SqlState == PostgresErrorCodes.UniqueViolation;
}
