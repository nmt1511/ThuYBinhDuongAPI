namespace ThuYBinhDuongAPI.Services
{
    /// <summary>
    /// Interface cho Reminder Service
    /// </summary>
    public interface IReminderService
    {
        /// <summary>
        /// Kiểm tra và gửi reminder cho các next appointments trong 7 ngày tới
        /// </summary>
        Task<int> CheckAndSendRemindersAsync();
        
        /// <summary>
        /// Kiểm tra và gửi reminder cho một user cụ thể
        /// </summary>
        Task<int> CheckAndSendRemindersForUserAsync(int userId);
    }
}

