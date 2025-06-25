using System.ComponentModel.DataAnnotations;

namespace ThuYBinhDuongAPI.Data.Dtos
{
    public class CreateAppointmentDto
    {
        [Required(ErrorMessage = "Thú cưng là bắt buộc")]
        public int PetId { get; set; }

        [Required(ErrorMessage = "Dịch vụ là bắt buộc")]
        public int ServiceId { get; set; }

        public int? DoctorId { get; set; } // Tùy chọn, có thể để admin chọn sau

        [Required(ErrorMessage = "Ngày hẹn là bắt buộc")]
        [DataType(DataType.Date, ErrorMessage = "Định dạng ngày không hợp lệ")]
        public DateOnly AppointmentDate { get; set; }

        [Required(ErrorMessage = "Giờ hẹn là bắt buộc")]
        [StringLength(50, ErrorMessage = "Giờ hẹn không được vượt quá 50 ký tự")]
        public string AppointmentTime { get; set; } = null!;

        [Range(0.1, 100, ErrorMessage = "Cân nặng phải từ 0.1 đến 100 kg")]
        public double? Weight { get; set; }

        [Range(0, 50, ErrorMessage = "Tuổi phải từ 0 đến 50")]
        public int? Age { get; set; }

        public bool? IsNewPet { get; set; } = false; // Mặc định là thú cưng cũ

        [StringLength(500, ErrorMessage = "Ghi chú không được vượt quá 500 ký tự")]
        public string? Notes { get; set; }
    }
} 