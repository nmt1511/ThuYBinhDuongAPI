namespace ThuYBinhDuongAPI.Data.Dtos
{
    public class UserResponseDto
    {
        public int UserId { get; set; }
        public string Username { get; set; } = null!;
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public int Role { get; set; }
        public string RoleName => Role switch
        {
            0 => "Customer",
            1 => "Doctor", 
            2 => "Admin",
            _ => "Unknown"
        };
        public DateTime? CreatedAt { get; set; }
        public string? Token { get; set; }
    }
} 