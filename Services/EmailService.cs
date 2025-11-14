using System.Net;
using System.Net.Mail;

namespace ThuYBinhDuongAPI.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task SendAppointmentConfirmationEmailAsync(string customerEmail, string customerName, string petName,
            string serviceName, string doctorName, string appointmentDate, string appointmentTime)
        {
            try
            {
                var subject = "Xác nhận lịch hẹn - Thú Y Bình Dương";
                var body = GenerateConfirmationEmailBody(customerName, petName, serviceName, doctorName, appointmentDate, appointmentTime);

                await SendEmailAsync(customerEmail, subject, body);
                _logger.LogInformation($"Sent appointment confirmation email to {customerEmail}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send appointment confirmation email to {customerEmail}");
                throw;
            }
        }

        public async Task SendAppointmentStatusChangeEmailAsync(string customerEmail, string customerName, string petName,
            string serviceName, string appointmentDate, string appointmentTime, string oldStatus, string newStatus)
        {
            try
            {
                var subject = "Cập nhật trạng thái lịch hẹn - Thú Y Bình Dương";
                var body = GenerateStatusChangeEmailBody(customerName, petName, serviceName, appointmentDate, appointmentTime, oldStatus, newStatus);

                await SendEmailAsync(customerEmail, subject, body);
                _logger.LogInformation($"Sent appointment status change email to {customerEmail}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send appointment status change email to {customerEmail}");
                throw;
            }
        }

        public async Task SendAppointmentReminderEmailAsync(string customerEmail, string customerName, string petName,
            string serviceName, string appointmentDate, string appointmentTime, int daysUntil, string? reminderNote)
        {
            try
            {
                var subject = daysUntil switch
                {
                    0 => "🔔 Nhắc hẹn: Hôm nay có lịch tái khám - Thú Y Bình Dương",
                    1 => "🔔 Nhắc hẹn: Ngày mai có lịch tái khám - Thú Y Bình Dương",
                    _ => $"🔔 Nhắc hẹn: Còn {daysUntil} ngày tới lịch tái khám - Thú Y Bình Dương"
                };

                var body = GenerateReminderEmailBody(customerName, petName, serviceName, appointmentDate, appointmentTime, daysUntil, reminderNote);

                await SendEmailAsync(customerEmail, subject, body);
                _logger.LogInformation($"Sent appointment reminder email to {customerEmail} ({daysUntil} days until appointment)");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send appointment reminder email to {customerEmail}");
                throw;
            }
        }

        private async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            var smtpHost = _configuration["Email:SmtpHost"] ?? "smtp.gmail.com";
            var smtpPort = int.Parse(_configuration["Email:SmtpPort"] ?? "587");
            var fromEmail = _configuration["Email:FromEmail"] ?? "";
            var fromPassword = _configuration["Email:FromPassword"] ?? "";
            var fromName = _configuration["Email:FromName"] ?? "Thú Y Bình Dương";

            if (string.IsNullOrEmpty(fromEmail) || string.IsNullOrEmpty(fromPassword))
            {
                _logger.LogWarning("Email configuration is missing. Email not sent.");
                return;
            }

            using var client = new SmtpClient(smtpHost, smtpPort);
            client.EnableSsl = true;
            client.Credentials = new NetworkCredential(fromEmail, fromPassword);

            var mailMessage = new MailMessage
            {
                From = new MailAddress(fromEmail, fromName),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };

            mailMessage.To.Add(toEmail);

            await client.SendMailAsync(mailMessage);
        }

        private string GenerateConfirmationEmailBody(string customerName, string petName, string serviceName, 
            string doctorName, string appointmentDate, string appointmentTime)
        {
            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>Xác nhận lịch hẹn</title>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #4CAF50; color: white; padding: 20px; text-align: center; }}
        .content {{ padding: 20px; background-color: #f9f9f9; }}
        .appointment-details {{ background-color: white; padding: 15px; border-left: 4px solid #4CAF50; margin: 15px 0; }}
        .footer {{ text-align: center; padding: 20px; color: #666; }}
        .status {{ color: #4CAF50; font-weight: bold; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>Xác nhận lịch hẹn</h1>
        </div>
        <div class='content'>
            <p>Xin chào <strong>{customerName}</strong>,</p>
            <p>Lịch hẹn của bạn đã được <span class='status'>XÁC NHẬN</span>. Dưới đây là thông tin chi tiết:</p>
            
            <div class='appointment-details'>
                <h3>Thông tin lịch hẹn</h3>
                <p><strong>Thú cưng:</strong> {petName}</p>
                <p><strong>Dịch vụ:</strong> {serviceName}</p>
                <p><strong>Bác sĩ:</strong> {doctorName}</p>
                <p><strong>Ngày hẹn:</strong> {appointmentDate}</p>
                <p><strong>Giờ hẹn:</strong> {appointmentTime}</p>
                <p><strong>Trạng thái:</strong> <span class='status'>Đã xác nhận</span></p>
            </div>
            
            <p><strong>Lưu ý quan trọng:</strong></p>
            <ul>
                <li>Vui lòng đến đúng giờ hẹn</li>
                <li>Mang theo thú cưng và các giấy tờ liên quan</li>
                <li>Nếu có thay đổi, vui lòng liên hệ trước ít nhất 2 giờ</li>
            </ul>
            
            <p>Cảm ơn bạn đã tin tưởng dịch vụ của chúng tôi!</p>
        </div>
        <div class='footer'>
            <p>Thú Y Bình Dương<br>
            Hotline: 0123456789<br>
            Email: info@thuybinhduong.com</p>
        </div>
    </div>
</body>
</html>";
        }

        private string GenerateStatusChangeEmailBody(string customerName, string petName, string serviceName,
            string appointmentDate, string appointmentTime, string oldStatus, string newStatus)
        {
            var statusColor = newStatus switch
            {
                "Đã xác nhận" => "#4CAF50",
                "Hoàn thành" => "#2196F3",
                "Đã hủy" => "#f44336",
                _ => "#666"
            };

            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>Cập nhật trạng thái lịch hẹn</title>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #2196F3; color: white; padding: 20px; text-align: center; }}
        .content {{ padding: 20px; background-color: #f9f9f9; }}
        .appointment-details {{ background-color: white; padding: 15px; border-left: 4px solid #2196F3; margin: 15px 0; }}
        .footer {{ text-align: center; padding: 20px; color: #666; }}
        .status {{ color: {statusColor}; font-weight: bold; }}
        .old-status {{ color: #999; text-decoration: line-through; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>Cập nhật trạng thái lịch hẹn</h1>
        </div>
        <div class='content'>
            <p>Xin chào <strong>{customerName}</strong>,</p>
            <p>Trạng thái lịch hẹn của bạn đã được cập nhật:</p>
            
            <div class='appointment-details'>
                <h3>Thông tin lịch hẹn</h3>
                <p><strong>Thú cưng:</strong> {petName}</p>
                <p><strong>Dịch vụ:</strong> {serviceName}</p>
                <p><strong>Ngày hẹn:</strong> {appointmentDate}</p>
                <p><strong>Giờ hẹn:</strong> {appointmentTime}</p>
                <p><strong>Trạng thái cũ:</strong> <span class='old-status'>{oldStatus}</span></p>
                <p><strong>Trạng thái mới:</strong> <span class='status'>{newStatus}</span></p>
            </div>
            
            <p>Nếu có bất kỳ thắc mắc nào, vui lòng liên hệ với chúng tôi.</p>
        </div>
        <div class='footer'>
            <p>Thú Y Bình Dương<br>
            Hotline: 0123456789<br>
            Email: info@thuybinhduong.com</p>
        </div>
    </div>
</body>
</html>";
        }

        private string GenerateReminderEmailBody(string customerName, string petName, string serviceName,
            string appointmentDate, string appointmentTime, int daysUntil, string? reminderNote)
        {
            var urgencyClass = daysUntil switch
            {
                0 => "urgent-today",
                1 => "urgent-tomorrow",
                _ => "reminder"
            };

            var urgencyColor = daysUntil switch
            {
                0 => "#E74C3C",
                1 => "#F39C12",
                _ => "#3498DB"
            };

            var mainMessage = daysUntil switch
            {
                0 => $"<p class='urgent-message'>Thú cưng <strong>{petName}</strong> của bạn có lịch tái khám <strong>HÔM NAY</strong>!</p>",
                1 => $"<p class='urgent-message'>Thú cưng <strong>{petName}</strong> của bạn có lịch tái khám <strong>NGÀY MAI</strong>!</p>",
                _ => $"<p>Đây là lời nhắc lịch tái khám cho thú cưng <strong>{petName}</strong> của bạn.</p>"
            };

            var reminderNoteHtml = !string.IsNullOrEmpty(reminderNote)
                ? $@"<div class='reminder-note'>
                    <h4>⚠️ Lưu ý quan trọng:</h4>
                    <p>{reminderNote}</p>
                </div>"
                : "";

            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>Nhắc hẹn tái khám</title>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: {urgencyColor}; color: white; padding: 20px; text-align: center; border-radius: 8px 8px 0 0; }}
        .header h1 {{ margin: 0; font-size: 24px; }}
        .content {{ padding: 20px; background-color: #f9f9f9; }}
        .urgent-message {{ font-size: 18px; color: {urgencyColor}; text-align: center; padding: 15px; background-color: #fff; border-radius: 8px; margin: 15px 0; }}
        .appointment-details {{ background-color: white; padding: 20px; border-left: 4px solid {urgencyColor}; margin: 15px 0; border-radius: 4px; box-shadow: 0 2px 4px rgba(0,0,0,0.1); }}
        .appointment-details h3 {{ margin-top: 0; color: {urgencyColor}; }}
        .appointment-details p {{ margin: 10px 0; }}
        .reminder-note {{ background-color: #FFF9E6; border: 2px solid #F39C12; padding: 15px; margin: 15px 0; border-radius: 4px; }}
        .reminder-note h4 {{ margin-top: 0; color: #F39C12; }}
        .preparation {{ background-color: #E8F5E9; padding: 15px; margin: 15px 0; border-radius: 4px; }}
        .preparation h4 {{ margin-top: 0; color: #4CAF50; }}
        .preparation ul {{ margin: 10px 0; padding-left: 20px; }}
        .footer {{ text-align: center; padding: 20px; color: #666; background-color: #f0f0f0; border-radius: 0 0 8px 8px; margin-top: 20px; }}
        .footer p {{ margin: 5px 0; }}
        .countdown {{ text-align: center; font-size: 36px; font-weight: bold; color: {urgencyColor}; margin: 20px 0; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>🔔 Nhắc nhở lịch tái khám</h1>
        </div>
        <div class='content'>
            <p>Xin chào <strong>{customerName}</strong>,</p>
            {mainMessage}
            
            {(daysUntil > 1 ? $"<div class='countdown'>Còn {daysUntil} ngày</div>" : "")}
            
            <div class='appointment-details'>
                <h3>📋 Thông tin lịch hẹn</h3>
                <p>🐾 <strong>Thú cưng:</strong> {petName}</p>
                <p>💊 <strong>Dịch vụ:</strong> {serviceName}</p>
                <p>📅 <strong>Ngày hẹn:</strong> {appointmentDate}</p>
                <p>⏰ <strong>Giờ hẹn:</strong> {appointmentTime}</p>
            </div>
            
            {reminderNoteHtml}
            
            <div class='preparation'>
                <h4>✅ Chuẩn bị trước khi đến:</h4>
                <ul>
                    <li>Vui lòng đến đúng giờ hẹn</li>
                    <li>Mang theo thú cưng và các giấy tờ liên quan (nếu có)</li>
                    <li>Chuẩn bị các câu hỏi bạn muốn hỏi bác sĩ</li>
                    <li>Nếu cần thay đổi lịch hẹn, vui lòng liên hệ trước ít nhất 2 giờ</li>
                </ul>
            </div>
            
            <p style='text-align: center; margin-top: 20px;'>
                <strong>Cảm ơn bạn đã tin tưởng dịch vụ của chúng tôi!</strong>
            </p>
        </div>
        <div class='footer'>
            <p><strong>Thú Y Bình Dương</strong></p>
            <p>📞 Hotline: 0123456789</p>
            <p>✉️ Email: info@thuybinhduong.com</p>
            <p>🌐 Website: www.thuybinhduong.com</p>
        </div>
    </div>
</body>
</html>";
        }

        /// <summary>
        /// Gửi email nhắc hẹn tái khám
        /// </summary>
        public async Task SendReminderEmailAsync(string customerEmail, string customerName, string petName,
            string serviceName, string nextAppointmentDate, string reminderNote)
        {
            try
            {
                var subject = $"Nhắc hẹn tái khám - {petName} - {nextAppointmentDate}";
                var body = GenerateReminderEmailBody(customerName, petName, serviceName, nextAppointmentDate, reminderNote);

                await SendEmailAsync(customerEmail, subject, body);
                _logger.LogInformation($"Sent reminder email to {customerEmail} for pet {petName} on {nextAppointmentDate}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending reminder email to {customerEmail}");
                throw;
            }
        }

        private string GenerateReminderEmailBody(string customerName, string petName, string serviceName,
            string nextAppointmentDate, string reminderNote)
        {
            var reminderNoteHtml = !string.IsNullOrEmpty(reminderNote)
                ? $@"<div class='reminder-note'>
                    <h4>📝 Ghi chú nhắc hẹn:</h4>
                    <p style='background: #FFF3CD; padding: 10px; border-left: 4px solid #FFC107; margin: 10px 0;'>
                        {reminderNote}
                    </p>
                </div>"
                : "";

            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Nhắc hẹn tái khám</title>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; margin: 0; padding: 0; background-color: #f4f4f4; }}
        .container {{ max-width: 600px; margin: 0 auto; background: white; padding: 20px; border-radius: 10px; box-shadow: 0 0 10px rgba(0,0,0,0.1); }}
        .header {{ background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 20px; text-align: center; border-radius: 10px 10px 0 0; margin: -20px -20px 20px -20px; }}
        .header h1 {{ margin: 0; font-size: 24px; }}
        .appointment-details {{ background: #f8f9fa; padding: 20px; border-radius: 8px; margin: 20px 0; border-left: 4px solid #007bff; }}
        .appointment-details h3 {{ color: #007bff; margin-top: 0; }}
        .appointment-details p {{ margin: 10px 0; }}
        .reminder-note {{ background: #FFF3CD; padding: 15px; border-radius: 8px; margin: 15px 0; border-left: 4px solid #FFC107; }}
        .preparation {{ background: #E8F5E8; padding: 15px; border-radius: 8px; margin: 15px 0; }}
        .preparation h4 {{ color: #28a745; margin-top: 0; }}
        .preparation ul {{ margin: 10px 0; padding-left: 20px; }}
        .footer {{ text-align: center; margin-top: 30px; padding: 20px; background: #f8f9fa; border-radius: 8px; }}
        .urgent-message {{ background: #FFE6E6; padding: 15px; border-radius: 8px; border-left: 4px solid #DC3545; color: #721C24; font-weight: bold; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>🔔 Nhắc hẹn tái khám</h1>
            <p>Thú Y Bình Dương</p>
        </div>
        
        <div class='content'>
            <p>Xin chào <strong>{customerName}</strong>,</p>
            
            <p>Đây là lời nhắc hẹn tái khám cho thú cưng <strong>{petName}</strong> của bạn.</p>
            
            <div class='appointment-details'>
                <h3>📅 Thông tin nhắc hẹn</h3>
                <p><strong>Thú cưng:</strong> {petName}</p>
                <p><strong>Dịch vụ tái khám:</strong> {serviceName}</p>
                <p><strong>Ngày nhắc hẹn:</strong> {nextAppointmentDate}</p>
            </div>
            
            {reminderNoteHtml}
            
            <div class='preparation'>
                <h4>✅ Lưu ý quan trọng:</h4>
                <ul>
                    <li>Vui lòng liên hệ để đặt lịch hẹn cụ thể</li>
                    <li>Mang theo thú cưng và các giấy tờ liên quan (nếu có)</li>
                    <li>Chuẩn bị các câu hỏi bạn muốn hỏi bác sĩ</li>
                    <li>Nếu có thay đổi, vui lòng thông báo trước</li>
                </ul>
            </div>
            
            <p style='text-align: center; margin-top: 20px;'>
                <strong>Cảm ơn bạn đã tin tưởng dịch vụ của chúng tôi!</strong>
            </p>
        </div>
        <div class='footer'>
            <p><strong>Thú Y Bình Dương</strong></p>
            <p>📞 Hotline: 0123456789</p>
            <p>✉️ Email: info@thuybinhduong.com</p>
            <p>🌐 Website: www.thuybinhduong.com</p>
        </div>
    </div>
</body>
</html>";
        }
    }
} 