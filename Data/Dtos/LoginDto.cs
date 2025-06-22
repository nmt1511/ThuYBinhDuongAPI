using System.ComponentModel.DataAnnotations;

namespace ThuYBinhDuongAPI.Data.Dtos;

/// <summary>
/// DTO cho chức năng đăng nhập hệ thống
/// </summary>
public class LoginDto
{
    [Required(ErrorMessage = "Tên đăng nhập là bắt buộc")]
    [StringLength(50, ErrorMessage = "Tên đăng nhập không được vượt quá 50 ký tự")]
    public string Username { get; set; } = null!;

    [Required(ErrorMessage = "Mật khẩu là bắt buộc")]
    [StringLength(100, ErrorMessage = "Mật khẩu không được vượt quá 100 ký tự")]
    public string Password { get; set; } = null!;
} 