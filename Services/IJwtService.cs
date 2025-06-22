using ThuYBinhDuongAPI.Models;

namespace ThuYBinhDuongAPI.Services;

/// <summary>
/// Interface cho dịch vụ quản lý JWT token
/// </summary>
public interface IJwtService
{
    /// <summary>
    /// Tạo JWT token cho người dùng
    /// </summary>
    /// <param name="user">Thông tin người dùng</param>
    /// <param name="roleName">Tên vai trò</param>
    /// <returns>JWT token string</returns>
    string GenerateToken(User user, string roleName);

    /// <summary>
    /// Lấy thông tin người dùng từ token
    /// </summary>
    /// <param name="token">JWT token</param>
    /// <returns>User ID nếu token hợp lệ</returns>
    int? GetUserIdFromToken(string token);

    /// <summary>
    /// Kiểm tra token có hợp lệ không
    /// </summary>
    /// <param name="token">JWT token</param>
    /// <returns>True nếu token hợp lệ</returns>
    bool ValidateToken(string token);
} 