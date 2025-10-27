using System.Text.Json;

namespace ThuYBinhDuongAPI.Services
{
    /// <summary>
    /// Interface cho Notification Service (Local notifications)
    /// </summary>
    public interface INotificationService
    {
        /// <summary>
        /// Tạo notification mới cho user
        /// </summary>
        Task<bool> CreateNotificationAsync(int userId, string title, string body, string? type = null, object? data = null);
        
        /// <summary>
        /// Tạo notification khi lịch hẹn thay đổi trạng thái
        /// </summary>
        Task<bool> CreateAppointmentStatusChangeNotificationAsync(
            int userId, 
            string petName, 
            string serviceName, 
            string oldStatus, 
            string newStatus, 
            DateOnly appointmentDate, 
            string appointmentTime);
        
        /// <summary>
        /// Tạo notification xác nhận lịch hẹn
        /// </summary>
        Task<bool> CreateAppointmentConfirmationNotificationAsync(
            int userId, 
            string petName, 
            string serviceName, 
            string doctorName, 
            DateOnly appointmentDate, 
            string appointmentTime);
    }
}

