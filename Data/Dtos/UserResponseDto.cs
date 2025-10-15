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
            1 => "Administrator",
            2 => "Doctor",
            _ => "Unknown"
        };
        public DateTime? CreatedAt { get; set; }
        public string? Token { get; set; }
        
        // Customer-specific fields (only populated for customers)
        public int? CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public string? Address { get; set; }
        public int? Gender { get; set; }
    }
} 