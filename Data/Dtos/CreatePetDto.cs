using System.ComponentModel.DataAnnotations;

namespace ThuYBinhDuongAPI.Data.Dtos
{
    public class CreatePetDto
    {
        [Required(ErrorMessage = "Tên thú cưng là bắt buộc")]
        [StringLength(255, ErrorMessage = "Tên thú cưng không được vượt quá 255 ký tự")]
        public string Name { get; set; } = null!;

        [Required(ErrorMessage = "Loài thú cưng là bắt buộc")]
        [StringLength(100, ErrorMessage = "Loài không được vượt quá 100 ký tự")]
        public string Species { get; set; } = null!;

        [StringLength(100, ErrorMessage = "Giống không được vượt quá 100 ký tự")]
        public string? Breed { get; set; }

        [DataType(DataType.Date, ErrorMessage = "Định dạng ngày sinh không hợp lệ")]
        public DateOnly? BirthDate { get; set; }

        // Property để nhận ngày sinh từ string format (React Native)
        [StringLength(10, ErrorMessage = "Định dạng ngày sinh không hợp lệ")]
        public string? BirthDateString { get; set; }

        [StringLength(500, ErrorMessage = "URL hình ảnh không được vượt quá 500 ký tự")]
        public string? ImageUrl { get; set; }

        [StringLength(50, ErrorMessage = "Giới tính không được vượt quá 50 ký tự")]
        public string? Gender { get; set; }

        [StringLength(1000, ErrorMessage = "Thông tin vaccine đã tiêm không được vượt quá 1000 ký tự")]
        public string? VaccinatedVaccines { get; set; }
    }
} 