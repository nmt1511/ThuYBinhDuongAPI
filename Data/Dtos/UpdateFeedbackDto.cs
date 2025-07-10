using System.ComponentModel.DataAnnotations;

namespace ThuYBinhDuongAPI.Data.Dtos
{
    public class UpdateFeedbackDto
    {
        [Required(ErrorMessage = "Đánh giá là bắt buộc")]
        [Range(1, 5, ErrorMessage = "Đánh giá phải từ 1 đến 5 sao")]
        public int Rating { get; set; }

        [MaxLength(1000, ErrorMessage = "Nhận xét không được vượt quá 1000 ký tự")]
        public string? Comment { get; set; }
    }
} 