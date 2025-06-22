namespace ThuYBinhDuongAPI.Data.Dtos;

/// <summary>
/// DTO trả về thông tin người dùng sau khi đăng ký/đăng nhập thành công
/// </summary>
public class UserResponseDto
{
    public int UserId { get; set; }
    public string Username { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string? PhoneNumber { get; set; }
    public int Role { get; set; }
    public string RoleName { get; set; } = null!;
    public DateTime? CreatedAt { get; set; }
    public string? Token { get; set; } // Để sau này thêm JWT token
} 