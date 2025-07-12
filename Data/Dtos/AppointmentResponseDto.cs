namespace ThuYBinhDuongAPI.Data.Dtos
{
    public class AppointmentResponseDto
    {
        public int AppointmentId { get; set; }
        public int PetId { get; set; }
        public int? DoctorId { get; set; }
        public int ServiceId { get; set; }
        public DateOnly AppointmentDate { get; set; }
        public string AppointmentTime { get; set; } = null!;
        public double? Weight { get; set; }
        public int? Age { get; set; }
        public bool? IsNewPet { get; set; }
        public int? Status { get; set; }
        public string? Notes { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string? VaccinatedVaccines { get; set; }

        // Thông tin liên quan
        public string? PetName { get; set; }
        public string? CustomerName { get; set; }
        public string? DoctorName { get; set; }
        public string? ServiceName { get; set; }
        public string? ServiceDescription { get; set; }

        // Thông tin trạng thái
        public string? StatusText { get; set; } // Chờ xác nhận, Đã xác nhận, etc.
        public bool CanCancel { get; set; } // Có thể hủy hay không
    }
} 