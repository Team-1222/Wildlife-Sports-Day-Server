using Microsoft.EntityFrameworkCore;
using Wildlife_Sports_Day_Server.Entities;
using Wildlife_Sports_Day_Server.Infrastructure;

namespace Wildlife_Sports_Day_Server.Repositories;

public class UserRepository(AppDbContext dbContext) : IUserRepository
{
    public async Task<bool> ExistsByEmailAsync(string email) =>
        await dbContext.Users.AnyAsync(user => user.Email == email);

    public async Task<User?> FindByEmailAsync(string email) =>
        await dbContext.Users.FirstOrDefaultAsync(user => user.Email == email);

    public async Task<User> SaveAsync(User user)
    {
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
        return user;
    }
}
