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
    }
} 