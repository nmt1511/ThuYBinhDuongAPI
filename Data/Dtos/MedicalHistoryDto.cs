namespace ThuYBinhDuongAPI.Data.Dtos
{
    public class MedicalHistoryDto
    {
        public int HistoryId { get; set; }
        public int PetId { get; set; }
        public DateTime? RecordDate { get; set; }
        public string? Description { get; set; }
        public string? Treatment { get; set; }
        public string? Notes { get; set; }
    }
} 