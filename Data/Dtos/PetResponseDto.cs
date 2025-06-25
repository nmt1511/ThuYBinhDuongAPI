namespace ThuYBinhDuongAPI.Data.Dtos
{
    public class PetResponseDto
    {
        public int PetId { get; set; }
        public int CustomerId { get; set; }
        public string Name { get; set; } = null!;
        public string? Species { get; set; }
        public string? Breed { get; set; }
        public DateOnly? BirthDate { get; set; }
        public string? ImageUrl { get; set; }
        
        // Thông tin bổ sung
        public int? Age { get; set; } // Tuổi tính theo năm
        public string? CustomerName { get; set; } // Tên chủ thú cưng
    }
} 