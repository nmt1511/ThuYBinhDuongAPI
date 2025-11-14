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
        
        /// <summary>
        /// Lấy danh sách reminders sắp tới (không gửi, chỉ để xem)
        /// </summary>
        Task<List<object>> GetUpcomingRemindersAsync();
        
        /// <summary>
        /// Lấy danh sách reminders cho user cụ thể
        /// </summary>
        Task<List<object>> GetUserRemindersAsync(int userId);
        
        /// <summary>
        /// Lấy danh sách users có reminders sắp tới
        /// </summary>
        Task<List<object>> GetUsersWithRemindersAsync();
        
        /// <summary>
        /// Reset trạng thái gửi reminders (để test)
        /// </summary>
        Task<int> ResetReminderStatusAsync();
    }
}




