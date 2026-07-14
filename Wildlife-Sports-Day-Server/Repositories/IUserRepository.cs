using Wildlife_Sports_Day_Server.Entities;

namespace Wildlife_Sports_Day_Server.Repositories;

public interface IUserRepository
{
    Task<bool> ExistsByEmailAsync(string email);
    Task<bool> ExistsByNicknameAsync(string nickname);
    Task<User?> FindByEmailAsync(string email);
    Task<User?> FindByNicknameAsync(string nickname);
    Task<User> SaveAsync(User user);
    Task<User?> SaveIfUniqueAsync(User user);
}
