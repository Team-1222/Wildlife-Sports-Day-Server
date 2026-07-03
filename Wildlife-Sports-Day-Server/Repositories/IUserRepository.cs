using Wildlife_Sports_Day_Server.Entities;

namespace Wildlife_Sports_Day_Server.Repositories;

public interface IUserRepository
{
    Task<bool> ExistsByEmailAsync(string email);
    Task<User?> FindByEmailAsync(string email);
    Task<User> SaveAsync(User user);
}
