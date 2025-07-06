namespace ThuYBinhDuongAPI.Data.Dtos;

public class DashboardAnalyticsDto
{
    public string Period { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public OverallStatsDto OverallStats { get; set; } = new();
    public List<TimeSeriesDataDto> TimeSeriesData { get; set; } = new();
    public List<DoctorAnalysisDto> DoctorAnalysis { get; set; } = new();
    public List<ServiceAnalysisDto> ServiceAnalysis { get; set; } = new();
}

public class OverallStatsDto
{
    public int TotalAppointments { get; set; }
    public int CompletedAppointments { get; set; }
    public int CancelledAppointments { get; set; }
    public int NoShowAppointments { get; set; }
    public double CompletionRate { get; set; }
    public decimal TotalRevenue { get; set; }
    public int TotalCustomers { get; set; }
    public int TotalPets { get; set; }
    public int TotalDoctors { get; set; }
}

public class TimeSeriesDataDto
{
    public string Date { get; set; } = string.Empty;
    public int TotalAppointments { get; set; }
    public int CompletedAppointments { get; set; }
    public int CancelledAppointments { get; set; }
    public double CompletionRate { get; set; }
}

public class DoctorAnalysisDto
{
    public int DoctorId { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public int TotalAppointments { get; set; }
    public int CompletedAppointments { get; set; }
    public int CancelledAppointments { get; set; }
    public double CompletionRate { get; set; }
}

public class ServiceAnalysisDto
{
    public int ServiceId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public int TotalAppointments { get; set; }
    public int CompletedAppointments { get; set; }
    public int CancelledAppointments { get; set; }
    public double CompletionRate { get; set; }
}

public class CompletionTrendDto
{
    public string Period { get; set; } = string.Empty;
    public int TotalAppointments { get; set; }
    public int CompletedAppointments { get; set; }
    public double CompletionRate { get; set; }
}

public class PeriodPerformanceDto
{
    public string Period { get; set; } = string.Empty;
    public PeriodDataDto CurrentPeriod { get; set; } = new();
    public PeriodDataDto PreviousPeriod { get; set; } = new();
    public ChangeDataDto Change { get; set; } = new();
}

public class PeriodDataDto
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int TotalAppointments { get; set; }
    public int CompletedAppointments { get; set; }
    public double CompletionRate { get; set; }
}

public class ChangeDataDto
{
    public int AppointmentChange { get; set; }
    public double CompletionRateChange { get; set; }
    public double AppointmentChangePercent { get; set; }
}

public class SimpleDashboardDto
{
    public TodayStatsDto TodayStats { get; set; } = new();
    public List<AppointmentDetailDto> TodayAppointments { get; set; } = new();
    public CompletionStatsDto CompletionStats { get; set; } = new();
}

public class TodayStatsDto
{
    public int TotalAppointments { get; set; }
    public int PendingAppointments { get; set; }
    public int ConfirmedAppointments { get; set; }
    public int CompletedAppointments { get; set; }
    public int CancelledAppointments { get; set; }
}

public class CompletionStatsDto
{
    public int TotalAppointments { get; set; }
    public int CompletedAppointments { get; set; }
    public int CancelledAppointments { get; set; }
    public double CompletionRate { get; set; }
    public double CancellationRate { get; set; }
}

public class AppointmentDetailDto
{
    public int AppointmentId { get; set; }
    public string Time { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string PetName { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public string DoctorName { get; set; } = string.Empty;
    public int Status { get; set; }
    public string StatusText { get; set; } = string.Empty;
} 