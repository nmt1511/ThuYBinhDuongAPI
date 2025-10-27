using Microsoft.EntityFrameworkCore;
using ThuYBinhDuongAPI.Models;

namespace ThuYBinhDuongAPI.Services
{
    /// <summary>
    /// Service để kiểm tra và gửi reminder cho next appointments
    /// </summary>
    public class ReminderService : IReminderService
    {
        private readonly ThuybinhduongContext _context;
        private readonly INotificationService _notificationService;
        private readonly IEmailService _emailService;
        private readonly ILogger<ReminderService> _logger;

        public ReminderService(
            ThuybinhduongContext context,
            INotificationService notificationService,
            IEmailService emailService,
            ILogger<ReminderService> logger)
        {
            _context = context;
            _notificationService = notificationService;
            _emailService = emailService;
            _logger = logger;
        }

        /// <summary>
        /// Kiểm tra và gửi reminder cho các next appointments trong 7 ngày tới
        /// </summary>
        public async Task<int> CheckAndSendRemindersAsync()
        {
            try
            {
                var today = DateTime.Today;
                var sevenDaysLater = today.AddDays(7);

                _logger.LogInformation($"Checking reminders from {today:yyyy-MM-dd} to {sevenDaysLater:yyyy-MM-dd}");

                // Lấy các medical histories có next_appointment_date trong vòng 7 ngày
                // và chưa gửi reminder
                var upcomingReminders = await _context.MedicalHistories
                    .Include(mh => mh.Pet)
                        .ThenInclude(p => p.Customer)
                            .ThenInclude(c => c.User)
                    .Include(mh => mh.NextService)
                    .Where(mh => 
                        mh.NextAppointmentDate.HasValue &&
                        mh.NextAppointmentDate.Value.Date >= today &&
                        mh.NextAppointmentDate.Value.Date <= sevenDaysLater &&
                        (!mh.ReminderSent.HasValue || mh.ReminderSent == false))
                    .ToListAsync();

                _logger.LogInformation($"Found {upcomingReminders.Count} reminders to send");

                int sentCount = 0;

                foreach (var medicalHistory in upcomingReminders)
                {
                    try
                    {
                        var userId = medicalHistory.Pet.Customer.UserId;
                        var userEmail = medicalHistory.Pet.Customer.User.Email;
                        var customerName = medicalHistory.Pet.Customer.CustomerName;
                        var daysUntil = (medicalHistory.NextAppointmentDate.Value.Date - today).Days;
                        var petName = medicalHistory.Pet.Name;
                        var serviceName = medicalHistory.NextService?.Name ?? "Tái khám";
                        var appointmentDate = medicalHistory.NextAppointmentDate.Value.ToString("dd/MM/yyyy");
                        var appointmentTime = medicalHistory.NextAppointmentDate.Value.ToString("HH:mm");

                        // Tạo title và body dựa trên số ngày còn lại
                        string title, body;
                        if (daysUntil == 0)
                        {
                            title = "🔔 Nhắc hẹn: Hôm nay có lịch tái khám";
                            body = $"{petName} có lịch {serviceName} HÔM NAY ({appointmentDate} lúc {appointmentTime}). Vui lòng đến đúng giờ!";
                        }
                        else if (daysUntil == 1)
                        {
                            title = "🔔 Nhắc hẹn: Ngày mai có lịch tái khám";
                            body = $"{petName} có lịch {serviceName} NGÀY MAI ({appointmentDate} lúc {appointmentTime}). Hãy chuẩn bị sẵn sàng!";
                        }
                        else
                        {
                            title = $"🔔 Nhắc hẹn: Còn {daysUntil} ngày tới lịch tái khám";
                            body = $"{petName} có lịch {serviceName} vào ngày {appointmentDate} lúc {appointmentTime}. Đừng quên nhé!";
                        }

                        // Thêm reminder note nếu có
                        if (!string.IsNullOrEmpty(medicalHistory.ReminderNote))
                        {
                            body += $"\n\nLưu ý: {medicalHistory.ReminderNote}";
                        }

                        // Tạo notification
                        var notificationSuccess = await _notificationService.CreateNotificationAsync(
                            userId,
                            title,
                            body,
                            "appointment_reminder",
                            new
                            {
                                type = "appointment_reminder",
                                medicalHistoryId = medicalHistory.HistoryId,
                                petId = medicalHistory.PetId,
                                petName = petName,
                                serviceName = serviceName,
                                appointmentDate = appointmentDate,
                                appointmentTime = appointmentTime,
                                daysUntil = daysUntil,
                                reminderNote = medicalHistory.ReminderNote
                            }
                        );

                        if (notificationSuccess)
                        {
                            // Gửi email reminder
                            try
                            {
                                if (!string.IsNullOrEmpty(userEmail))
                                {
                                    await _emailService.SendAppointmentReminderEmailAsync(
                                        userEmail,
                                        customerName,
                                        petName,
                                        serviceName,
                                        appointmentDate,
                                        appointmentTime,
                                        daysUntil,
                                        medicalHistory.ReminderNote
                                    );
                                    _logger.LogInformation($"Sent reminder email to {userEmail} for medical history {medicalHistory.HistoryId}");
                                }
                            }
                            catch (Exception emailEx)
                            {
                                _logger.LogError(emailEx, $"Failed to send reminder email to {userEmail}, but notification was created");
                            }

                            // Đánh dấu đã gửi reminder
                            medicalHistory.ReminderSent = true;
                            sentCount++;
                            _logger.LogInformation($"Sent reminder for medical history {medicalHistory.HistoryId}, pet {petName}, user {userId}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error sending reminder for medical history {medicalHistory.HistoryId}");
                    }
                }

                // Lưu changes
                if (sentCount > 0)
                {
                    await _context.SaveChangesAsync();
                    _logger.LogInformation($"Successfully sent {sentCount} reminders");
                }

                return sentCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking and sending reminders");
                return 0;
            }
        }

        /// <summary>
        /// Kiểm tra và gửi reminder cho một user cụ thể
        /// </summary>
        public async Task<int> CheckAndSendRemindersForUserAsync(int userId)
        {
            try
            {
                var today = DateTime.Today;
                var sevenDaysLater = today.AddDays(7);

                _logger.LogInformation($"Checking reminders for user {userId} from {today:yyyy-MM-dd} to {sevenDaysLater:yyyy-MM-dd}");

                // Lấy customer của user
                var customer = await _context.Customers
                    .FirstOrDefaultAsync(c => c.UserId == userId);

                if (customer == null)
                {
                    _logger.LogWarning($"No customer found for user {userId}");
                    return 0;
                }

                // Lấy các medical histories của pets thuộc customer này
                var upcomingReminders = await _context.MedicalHistories
                    .Include(mh => mh.Pet)
                    .Include(mh => mh.NextService)
                    .Where(mh =>
                        mh.Pet.CustomerId == customer.CustomerId &&
                        mh.NextAppointmentDate.HasValue &&
                        mh.NextAppointmentDate.Value.Date >= today &&
                        mh.NextAppointmentDate.Value.Date <= sevenDaysLater &&
                        (!mh.ReminderSent.HasValue || mh.ReminderSent == false))
                    .ToListAsync();

                _logger.LogInformation($"Found {upcomingReminders.Count} reminders for user {userId}");

                int sentCount = 0;

                foreach (var medicalHistory in upcomingReminders)
                {
                    try
                    {
                        // Load user info để lấy email
                        var customer = await _context.Customers
                            .Include(c => c.User)
                            .FirstOrDefaultAsync(c => c.CustomerId == medicalHistory.Pet.CustomerId);

                        if (customer?.User == null)
                        {
                            _logger.LogWarning($"Customer or user not found for pet {medicalHistory.Pet.PetId}");
                            continue;
                        }

                        var userEmail = customer.User.Email;
                        var customerName = customer.CustomerName;
                        var daysUntil = (medicalHistory.NextAppointmentDate.Value.Date - today).Days;
                        var petName = medicalHistory.Pet.Name;
                        var serviceName = medicalHistory.NextService?.Name ?? "Tái khám";
                        var appointmentDate = medicalHistory.NextAppointmentDate.Value.ToString("dd/MM/yyyy");
                        var appointmentTime = medicalHistory.NextAppointmentDate.Value.ToString("HH:mm");

                        string title, body;
                        if (daysUntil == 0)
                        {
                            title = "🔔 Nhắc hẹn: Hôm nay có lịch tái khám";
                            body = $"{petName} có lịch {serviceName} HÔM NAY ({appointmentDate} lúc {appointmentTime}). Vui lòng đến đúng giờ!";
                        }
                        else if (daysUntil == 1)
                        {
                            title = "🔔 Nhắc hẹn: Ngày mai có lịch tái khám";
                            body = $"{petName} có lịch {serviceName} NGÀY MAI ({appointmentDate} lúc {appointmentTime}). Hãy chuẩn bị sẵn sàng!";
                        }
                        else
                        {
                            title = $"🔔 Nhắc hẹn: Còn {daysUntil} ngày tới lịch tái khám";
                            body = $"{petName} có lịch {serviceName} vào ngày {appointmentDate} lúc {appointmentTime}. Đừng quên nhé!";
                        }

                        if (!string.IsNullOrEmpty(medicalHistory.ReminderNote))
                        {
                            body += $"\n\nLưu ý: {medicalHistory.ReminderNote}";
                        }

                        var notificationSuccess = await _notificationService.CreateNotificationAsync(
                            userId,
                            title,
                            body,
                            "appointment_reminder",
                            new
                            {
                                type = "appointment_reminder",
                                medicalHistoryId = medicalHistory.HistoryId,
                                petId = medicalHistory.PetId,
                                petName = petName,
                                serviceName = serviceName,
                                appointmentDate = appointmentDate,
                                appointmentTime = appointmentTime,
                                daysUntil = daysUntil,
                                reminderNote = medicalHistory.ReminderNote
                            }
                        );

                        if (notificationSuccess)
                        {
                            // Gửi email reminder
                            try
                            {
                                if (!string.IsNullOrEmpty(userEmail))
                                {
                                    await _emailService.SendAppointmentReminderEmailAsync(
                                        userEmail,
                                        customerName,
                                        petName,
                                        serviceName,
                                        appointmentDate,
                                        appointmentTime,
                                        daysUntil,
                                        medicalHistory.ReminderNote
                                    );
                                    _logger.LogInformation($"Sent reminder email to {userEmail} for medical history {medicalHistory.HistoryId}");
                                }
                            }
                            catch (Exception emailEx)
                            {
                                _logger.LogError(emailEx, $"Failed to send reminder email to {userEmail}, but notification was created");
                            }

                            medicalHistory.ReminderSent = true;
                            sentCount++;
                            _logger.LogInformation($"Sent reminder for medical history {medicalHistory.HistoryId} to user {userId}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error sending reminder for medical history {medicalHistory.HistoryId}");
                    }
                }

                if (sentCount > 0)
                {
                    await _context.SaveChangesAsync();
                    _logger.LogInformation($"Successfully sent {sentCount} reminders to user {userId}");
                }

                return sentCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking and sending reminders for user {userId}");
                return 0;
            }
        }
    }
}

