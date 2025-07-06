namespace ThuYBinhDuongAPI.Services
{
    public interface IEmailService
    {
        Task SendAppointmentConfirmationEmailAsync(string customerEmail, string customerName, string petName, 
            string serviceName, string doctorName, string appointmentDate, string appointmentTime);
        
        Task SendAppointmentStatusChangeEmailAsync(string customerEmail, string customerName, string petName,
            string serviceName, string appointmentDate, string appointmentTime, string oldStatus, string newStatus);
    }
} 