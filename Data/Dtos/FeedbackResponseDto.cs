namespace ThuYBinhDuongAPI.Data.Dtos
{
    public class FeedbackResponseDto
    {
        public int FeedbackId { get; set; }
        public int AppointmentId { get; set; }
        public int Rating { get; set; }
        public string? Comment { get; set; }
        public DateTime? CreatedAt { get; set; }
        
        // Appointment details
        public string? CustomerName { get; set; }
        public string? PetName { get; set; }
        public string? ServiceName { get; set; }
        public DateOnly? AppointmentDate { get; set; }
        public string? AppointmentTime { get; set; }
        public string? DoctorName { get; set; }
        
        // Helper properties
        public string RatingText => GetRatingText(Rating);
        public string RatingStars => new string('★', Rating) + new string('☆', 5 - Rating);
        
        private static string GetRatingText(int rating)
        {
            return rating switch
            {
                1 => "Rất không hài lòng",
                2 => "Không hài lòng",
                3 => "Bình thường",
                4 => "Hài lòng",
                5 => "Rất hài lòng",
                _ => "Chưa đánh giá"
            };
        }
    }
} 