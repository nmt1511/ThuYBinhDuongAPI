using ThuYBinhDuongAPI.Models;

namespace ThuYBinhDuongAPI.Services
{
    public interface IJwtService
    {
        string GenerateToken(User user);
        bool ValidateToken(string token);
        string? GetUserIdFromToken(string token);
        Task<int?> GetCustomerIdFromUserIdAsync(int userId);
    }
} 