using System.Text.Json;
using ThuYBinhDuongAPI.Models;

namespace ThuYBinhDuongAPI.Services
{
    /// <summary>
    /// Service để tạo và quản lý notifications (Local notification approach)
    /// </summary>
    public class NotificationService : INotificationService
    {
        private readonly ThuybinhduongContext _context;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(ThuybinhduongContext context, ILogger<NotificationService> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Tạo notification mới cho user
        /// </summary>
        public async Task<bool> CreateNotificationAsync(int userId, string title, string body, string? type = null, object? data = null)
        {
            try
            {
                var notification = new Notification
                {
                    UserId = userId,
                    Title = title,
                    Body = body,
                    Type = type,
                    Data = data != null ? JsonSerializer.Serialize(data) : null,
                    IsRead = false,
                    CreatedAt = DateTime.Now
                };

                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Created notification for user {userId}: {title}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating notification for user {userId}");
                return false;
            }
        }

        /// <summary>
        /// Tạo notification khi lịch hẹn thay đổi trạng thái
        /// </summary>
        public async Task<bool> CreateAppointmentStatusChangeNotificationAsync(
            int userId, 
            string petName, 
            string serviceName, 
            string oldStatus, 
            string newStatus, 
            DateOnly appointmentDate, 
            string appointmentTime)
        {
            var title = "Lịch hẹn thay đổi trạng thái";
            var body = $"Lịch hẹn của {petName} - {serviceName} đã thay đổi từ '{oldStatus}' sang '{newStatus}'";
            
            var data = new
            {
                type = "appointment_status_change",
                petName,
                serviceName,
                oldStatus,
                newStatus,
                appointmentDate = appointmentDate.ToString("dd/MM/yyyy"),
                appointmentTime
            };

            return await CreateNotificationAsync(userId, title, body, "appointment_status_change", data);
        }

        /// <summary>
        /// Tạo notification xác nhận lịch hẹn
        /// </summary>
        public async Task<bool> CreateAppointmentConfirmationNotificationAsync(
            int userId, 
            string petName, 
            string serviceName, 
            string doctorName, 
            DateOnly appointmentDate, 
            string appointmentTime)
        {
            var title = "Lịch hẹn đã được xác nhận";
            var body = $"Lịch hẹn của {petName} - {serviceName} vào {appointmentDate.ToString("dd/MM/yyyy")} lúc {appointmentTime} đã được xác nhận";
            
            var data = new
            {
                type = "appointment_confirmed",
                petName,
                serviceName,
                doctorName,
                appointmentDate = appointmentDate.ToString("dd/MM/yyyy"),
                appointmentTime
            };

            return await CreateNotificationAsync(userId, title, body, "appointment_confirmed", data);
        }
    }
}

