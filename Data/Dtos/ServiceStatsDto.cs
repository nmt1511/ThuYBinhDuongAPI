namespace ThuYBinhDuongAPI.Data.Dtos;

/// <summary>
/// DTO cho thống kê dịch vụ chi tiết
/// </summary>
public class ServiceStatsDto
{
    public int TotalServices { get; set; }
    public List<ServiceDetailStatsDto> TopServices { get; set; } = new();
    public List<ServiceRevenueDto> RevenueByService { get; set; } = new();
    public decimal TotalRevenue { get; set; }
    public decimal AverageServicePrice { get; set; }
}

/// <summary>
/// DTO cho thống kê chi tiết từng dịch vụ
/// </summary>
public class ServiceDetailStatsDto
{
    public int ServiceId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal? Price { get; set; }
    public int TotalAppointments { get; set; }
    public int CompletedAppointments { get; set; }
    public int CancelledAppointments { get; set; }
    public double CompletionRate { get; set; }
    public decimal TotalRevenue { get; set; }
}

/// <summary>
/// DTO cho doanh thu theo dịch vụ
/// </summary>
public class ServiceRevenueDto
{
    public int ServiceId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public int CompletedCount { get; set; }
    public double PercentageOfTotal { get; set; }
}

