using System.ComponentModel.DataAnnotations;

namespace ThuYBinhDuongAPI.Data.Dtos
{
    public class CreateDoctorDto
    {
        [Required(ErrorMessage = "Họ tên bác sĩ là bắt buộc")]
        [StringLength(100, ErrorMessage = "Họ tên không được vượt quá 100 ký tự")]
        public string FullName { get; set; } = null!;

        [StringLength(100, ErrorMessage = "Chuyên khoa không được vượt quá 100 ký tự")]
        public string? Specialization { get; set; }

        [Range(0, 50, ErrorMessage = "Số năm kinh nghiệm phải từ 0 đến 50")]
        public int? ExperienceYears { get; set; }

        [StringLength(100, ErrorMessage = "Chi nhánh không được vượt quá 100 ký tự")]
        public string? Branch { get; set; }
    }
} 