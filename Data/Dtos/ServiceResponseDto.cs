namespace ThuYBinhDuongAPI.Data.Dtos
{
    public class ServiceResponseDto
    {
        public int ServiceId { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public decimal? Price { get; set; }
        public int? Duration { get; set; } // Thời lượng (phút)
        public string? Category { get; set; }
        public bool? IsActive { get; set; }
        
        // Thông tin hiển thị
        public string DisplayText { get; set; } = null!; // "Tên dịch vụ - Giá (nếu có)"
        public string? PriceText { get; set; } // "500,000 VNĐ" hoặc "Liên hệ"
        public string? DurationText { get; set; } // "30 phút" hoặc "Liên hệ"
    }
} 