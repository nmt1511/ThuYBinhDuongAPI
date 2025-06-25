namespace ThuYBinhDuongAPI.Data.Dtos
{
    public class NewsResponseDto
    {
        public int NewsId { get; set; }
        public string Title { get; set; } = null!;
        public string? Content { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string? ImageUrl { get; set; }
        public string? Tags { get; set; }
        
        // Thông tin hiển thị
        public string? Summary { get; set; } // Tóm tắt nội dung (200 ký tự đầu)
        public string? CreatedAtText { get; set; } // "2 ngày trước", "1 tuần trước"
        public List<string> TagList { get; set; } = new List<string>(); // Tags được split thành array
    }
} 