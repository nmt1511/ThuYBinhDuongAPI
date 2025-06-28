using System.ComponentModel.DataAnnotations;

namespace ThuYBinhDuongAPI.Data.Dtos
{
    public class UpdatePetDto
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
    }
} 