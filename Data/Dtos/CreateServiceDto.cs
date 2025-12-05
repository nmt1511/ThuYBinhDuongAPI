using System.ComponentModel.DataAnnotations;

namespace ThuYBinhDuongAPI.Data.Dtos
{
    public class CreateServiceDto
    {
        [Required(ErrorMessage = "Tên dịch vụ là bắt buộc")]
        [StringLength(200, ErrorMessage = "Tên dịch vụ không được vượt quá 200 ký tự")]
        public string Name { get; set; } = null!;

        [StringLength(500, ErrorMessage = "Mô tả không được vượt quá 500 ký tự")]
        public string? Description { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Giá phải là số dương")]
        public decimal? Price { get; set; }

        [Range(1, 600, ErrorMessage = "Thời lượng phải từ 1 đến 600 phút")]
        public int? Duration { get; set; }

        [StringLength(100, ErrorMessage = "Danh mục không được vượt quá 100 ký tự")]
        public string? Category { get; set; }

        public bool? IsActive { get; set; }

        /// <summary>
        /// Số ngày chu kỳ khuyến nghị sử dụng lại dịch vụ
        /// VD: 60 (da liễu), 90 (spa/grooming), 180 (nha khoa), 365 (tiêm phòng)
        /// </summary>
        [Range(1, 3650, ErrorMessage = "Chu kỳ phải từ 1 đến 3650 ngày")]
        public int? RecurrenceDays { get; set; }
    }
} 