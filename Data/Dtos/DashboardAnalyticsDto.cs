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