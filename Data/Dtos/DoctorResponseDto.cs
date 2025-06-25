namespace ThuYBinhDuongAPI.Data.Dtos
{
    public class DoctorResponseDto
    {
        public int DoctorId { get; set; }
        public string FullName { get; set; } = null!;
        public string? Specialization { get; set; }
        public int? ExperienceYears { get; set; }
        public string? Branch { get; set; }
        
        // Thông tin hiển thị
        public string DisplayText { get; set; } = null!; // "Dr. Tên - Chuyên khoa"
    }
} 