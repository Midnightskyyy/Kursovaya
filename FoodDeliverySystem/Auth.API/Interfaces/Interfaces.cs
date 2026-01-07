using Auth.API.Entities;

namespace Auth.API.Interfaces
{
    public interface IUserRepository
    {
        Task<User> GetByIdAsync(Guid id);
        Task<User> GetByEmailAsync(string email);
        Task AddAsync(User user);
        Task UpdateAsync(User user);
        Task<bool> ExistsByEmailAsync(string email);
    }

    public interface ITokenService
    {
        string GenerateAccessToken(User user);
        string GenerateRefreshToken();
        Task<bool> ValidateRefreshTokenAsync(string token, Guid userId);
        Task SaveRefreshTokenAsync(Guid userId, string token);
        Task RevokeRefreshTokenAsync(string token);
    }
}
