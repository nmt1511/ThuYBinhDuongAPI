namespace ThuYBinhDuongAPI.Data.Dtos;

/// <summary>
/// DTO cho thống kê khách hàng chi tiết
/// </summary>
public class CustomerStatsDto
{
    public int TotalCustomers { get; set; }
    public int NewCustomers { get; set; }
    public int ReturningCustomers { get; set; }
    public double CustomerGrowth { get; set; }
    public double AverageAppointmentsPerCustomer { get; set; }
    public double AveragePetsPerCustomer { get; set; }
    public List<TopCustomerDto> TopCustomers { get; set; } = new();
    public List<CustomerActivityDto> CustomersByActivity { get; set; } = new();
}

/// <summary>
/// DTO cho top khách hàng
/// </summary>
public class TopCustomerDto
{
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public int TotalAppointments { get; set; }
    public int TotalPets { get; set; }
    public decimal TotalSpent { get; set; }
    public DateTime? LastVisit { get; set; }
}

/// <summary>
/// DTO cho hoạt động khách hàng
/// </summary>
public class CustomerActivityDto
{
    public string ActivityLevel { get; set; } = string.Empty; // High, Medium, Low
    public int CustomerCount { get; set; }
    public double Percentage { get; set; }
}

