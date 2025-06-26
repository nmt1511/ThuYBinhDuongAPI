using System.ComponentModel.DataAnnotations;

namespace ThuYBinhDuongAPI.Data.Dtos
{
    public class CreateNewsDto
    {
        [Required(ErrorMessage = "Tiêu đề tin tức là bắt buộc")]
        [StringLength(200, ErrorMessage = "Tiêu đề tin tức không được vượt quá 200 ký tự")]
        public string Title { get; set; } = null!;

        [Required(ErrorMessage = "Nội dung tin tức là bắt buộc")]
        public string Content { get; set; } = null!;

        [Url(ErrorMessage = "URL hình ảnh không hợp lệ")]
        public string? ImageUrl { get; set; }

        [StringLength(500, ErrorMessage = "Tags không được vượt quá 500 ký tự")]
        public string? Tags { get; set; }
    }
} 