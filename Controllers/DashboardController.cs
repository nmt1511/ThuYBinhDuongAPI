using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThuYBinhDuongAPI.Data.Dtos;
using ThuYBinhDuongAPI.Models;

namespace ThuYBinhDuongAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class DashboardController : ControllerBase
{
    private readonly ThuybinhduongContext _context;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(ThuybinhduongContext context, ILogger<DashboardController> logger)
    {
        _context = context;
        _logger = logger;
    }

    // GET: api/Dashboard/analytics
    [HttpGet("analytics")]
    [AuthorizeRole(1)] // Admin only
    public async Task<ActionResult<DashboardAnalyticsDto>> GetDashboardAnalytics([FromQuery] string period = "month")
    {
        try
        {
            var endDate = DateTime.Now;
            var startDate = period.ToLower() switch
            {
                "day" => endDate.AddDays(-1),
                "week" => endDate.AddDays(-7),
                "month" => endDate.AddMonths(-1),
                "quarter" => endDate.AddMonths(-3),
                "year" => endDate.AddYears(-1),
                _ => endDate.AddMonths(-1)
            };

            // Get appointments data
            var appointments = await _context.Appointments
                .Where(a => a.CreatedAt.HasValue && a.CreatedAt.Value >= startDate && a.CreatedAt.Value <= endDate)
                .Include(a => a.Doctor)
                .Include(a => a.Service)
                .Include(a => a.Pet)
                .ToListAsync();

            if (appointments == null)
            {
                _logger.LogWarning("No appointments found for the period");
                appointments = new List<Appointment>();
            }

            var totalAppointments = appointments.Count;
            var completedAppointments = appointments.Count(a => a.Status == 2); // Completed status
            var cancelledAppointments = appointments.Count(a => a.Status == 3); // Cancelled status
            var noShowAppointments = appointments.Count(a => a.Status == 4); // No-show status

            // Calculate completion rate
            var completionRate = totalAppointments > 0 ? (double)completedAppointments / totalAppointments * 100 : 0;

            // Group by time periods for trend analysis
            var timeSeriesData = await GetTimeSeriesData(startDate, endDate, period);

            // Analysis by doctor
            var doctorAnalysis = appointments
                .Where(a => a.Doctor != null && a.DoctorId.HasValue)
                .GroupBy(a => new { a.DoctorId, DoctorName = a.Doctor.FullName ?? "Unknown" })
                .Select(g => new DoctorAnalysisDto
                {
                    DoctorId = g.Key.DoctorId.Value,
                    DoctorName = g.Key.DoctorName,
                    TotalAppointments = g.Count(),
                    CompletedAppointments = g.Count(a => a.Status == 2),
                    CancelledAppointments = g.Count(a => a.Status == 3),
                    CompletionRate = g.Count() > 0 ? (double)g.Count(a => a.Status == 2) / g.Count() * 100 : 0
                })
                .OrderByDescending(d => d.CompletionRate)
                .ToList();

            // Analysis by service
            var serviceAnalysis = appointments
                .Where(a => a.Service != null && a.ServiceId > 0)
                .GroupBy(a => new { a.ServiceId, ServiceName = a.Service.Name ?? "Unknown" })
                .Select(g => new ServiceAnalysisDto
                {
                    ServiceId = g.Key.ServiceId,
                    ServiceName = g.Key.ServiceName,
                    TotalAppointments = g.Count(),
                    CompletedAppointments = g.Count(a => a.Status == 2),
                    CancelledAppointments = g.Count(a => a.Status == 3),
                    CompletionRate = g.Count() > 0 ? (double)g.Count(a => a.Status == 2) / g.Count() * 100 : 0
                })
                .OrderByDescending(s => s.CompletionRate)
                .ToList();

            // Additional statistics
            var totalRevenue = await CalculateRevenue(startDate, endDate);
            var totalCustomers = await _context.Users.CountAsync(u => u.Role == 0);
            var totalPets = await _context.Pets.CountAsync();
            var totalDoctors = await _context.Doctors.CountAsync();

            var analytics = new DashboardAnalyticsDto
            {
                Period = period,
                StartDate = startDate,
                EndDate = endDate,
                OverallStats = new OverallStatsDto
                {
                    TotalAppointments = totalAppointments,
                    CompletedAppointments = completedAppointments,
                    CancelledAppointments = cancelledAppointments,
                    NoShowAppointments = noShowAppointments,
                    CompletionRate = Math.Round(completionRate, 2),
                    TotalRevenue = totalRevenue,
                    TotalCustomers = totalCustomers,
                    TotalPets = totalPets,
                    TotalDoctors = totalDoctors
                },
                TimeSeriesData = timeSeriesData ?? new List<TimeSeriesDataDto>(),
                DoctorAnalysis = doctorAnalysis,
                ServiceAnalysis = serviceAnalysis
            };

            _logger.LogInformation($"Successfully retrieved analytics for period: {period}");
            return Ok(analytics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting dashboard analytics for period: {Period}", period);
            return StatusCode(500, new { message = "Lỗi server khi lấy dữ liệu thống kê", error = ex.Message });
        }
    }

    // GET: api/Dashboard/completion-trends
    [HttpGet("completion-trends")]
    [AuthorizeRole(1)] // Admin only
    public async Task<ActionResult<List<CompletionTrendDto>>> GetCompletionTrends([FromQuery] string period = "month", [FromQuery] int periods = 12)
    {
        try
        {
            if (periods <= 0 || periods > 100)
            {
                return BadRequest(new { message = "Số lượng kỳ phải từ 1 đến 100" });
            }

            var trends = new List<CompletionTrendDto>();
            var endDate = DateTime.Now;

            for (int i = periods - 1; i >= 0; i--)
            {
                var periodStart = period.ToLower() switch
                {
                    "day" => endDate.AddDays(-i),
                    "week" => endDate.AddDays(-i * 7),
                    "month" => endDate.AddMonths(-i),
                    "quarter" => endDate.AddMonths(-i * 3),
                    "year" => endDate.AddYears(-i),
                    _ => endDate.AddMonths(-i)
                };

                var periodEnd = period.ToLower() switch
                {
                    "day" => periodStart.AddDays(1),
                    "week" => periodStart.AddDays(7),
                    "month" => periodStart.AddMonths(1),
                    "quarter" => periodStart.AddMonths(3),
                    "year" => periodStart.AddYears(1),
                    _ => periodStart.AddMonths(1)
                };

                var appointmentsInPeriod = await _context.Appointments
                    .Where(a => a.CreatedAt.HasValue && a.CreatedAt.Value >= periodStart && a.CreatedAt.Value < periodEnd)
                    .ToListAsync();

                if (appointmentsInPeriod == null)
                {
                    _logger.LogWarning("No appointments found for period starting {PeriodStart}", periodStart);
                    appointmentsInPeriod = new List<Appointment>();
                }

                var totalInPeriod = appointmentsInPeriod.Count;
                var completedInPeriod = appointmentsInPeriod.Count(a => a.Status == 2);
                var completionRate = totalInPeriod > 0 ? (double)completedInPeriod / totalInPeriod * 100 : 0;

                var periodFormat = period.ToLower() switch
                {
                    "day" => "yyyy-MM-dd",
                    "week" => "yyyy-MM-dd",
                    "month" => "yyyy-MM",
                    "quarter" => "yyyy-MM",
                    "year" => "yyyy",
                    _ => "yyyy-MM"
                };

                trends.Add(new CompletionTrendDto
                {
                    Period = periodStart.ToString(periodFormat),
                    TotalAppointments = totalInPeriod,
                    CompletedAppointments = completedInPeriod,
                    CompletionRate = Math.Round(completionRate, 2)
                });
            }

            _logger.LogInformation("Successfully retrieved completion trends for {Period} with {Periods} periods", period, periods);
            return Ok(trends);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting completion trends for period: {Period}, periods: {Periods}", period, periods);
            return StatusCode(500, new { message = "Lỗi server khi lấy xu hướng hoàn thành", error = ex.Message });
        }
    }

    // GET: api/Dashboard/performance-by-period
    [HttpGet("performance-by-period")]
    [AuthorizeRole(1)] // Admin only
    public async Task<ActionResult<PeriodPerformanceDto>> GetPerformanceByPeriod([FromQuery] string period = "month")
    {
        try
        {
            var endDate = DateTime.Now;
            var startDate = period.ToLower() switch
            {
                "day" => endDate.AddDays(-1),
                "week" => endDate.AddDays(-7),
                "month" => endDate.AddMonths(-1),
                "quarter" => endDate.AddMonths(-3),
                "year" => endDate.AddYears(-1),
                _ => endDate.AddMonths(-1)
            };

            // Previous period for comparison
            var prevStartDate = period.ToLower() switch
            {
                "day" => startDate.AddDays(-1),
                "week" => startDate.AddDays(-7),
                "month" => startDate.AddMonths(-1),
                "quarter" => startDate.AddMonths(-3),
                "year" => startDate.AddYears(-1),
                _ => startDate.AddMonths(-1)
            };

            // Current period data
            var currentAppointments = await _context.Appointments
                .Where(a => a.CreatedAt.HasValue && a.CreatedAt.Value >= startDate && a.CreatedAt.Value <= endDate)
                .ToListAsync();

            // Previous period data
            var previousAppointments = await _context.Appointments
                .Where(a => a.CreatedAt.HasValue && a.CreatedAt.Value >= prevStartDate && a.CreatedAt.Value < startDate)
                .ToListAsync();

            var currentTotal = currentAppointments.Count;
            var currentCompleted = currentAppointments.Count(a => a.Status == 2);
            var currentRate = currentTotal > 0 ? (double)currentCompleted / currentTotal * 100 : 0;

            var previousTotal = previousAppointments.Count;
            var previousCompleted = previousAppointments.Count(a => a.Status == 2);
            var previousRate = previousTotal > 0 ? (double)previousCompleted / previousTotal * 100 : 0;

            var performance = new PeriodPerformanceDto
            {
                Period = period,
                CurrentPeriod = new PeriodDataDto
                {
                    StartDate = startDate,
                    EndDate = endDate,
                    TotalAppointments = currentTotal,
                    CompletedAppointments = currentCompleted,
                    CompletionRate = Math.Round(currentRate, 2)
                },
                PreviousPeriod = new PeriodDataDto
                {
                    StartDate = prevStartDate,
                    EndDate = startDate,
                    TotalAppointments = previousTotal,
                    CompletedAppointments = previousCompleted,
                    CompletionRate = Math.Round(previousRate, 2)
                },
                Change = new ChangeDataDto
                {
                    AppointmentChange = currentTotal - previousTotal,
                    CompletionRateChange = Math.Round(currentRate - previousRate, 2),
                    AppointmentChangePercent = previousTotal > 0 ? Math.Round((double)(currentTotal - previousTotal) / previousTotal * 100, 2) : 0
                }
            };

            return Ok(performance);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting period performance");
            return StatusCode(500, new { message = "Lỗi server khi lấy hiệu suất theo thời kỳ", error = ex.Message });
        }
    }

    // GET: api/Dashboard/today
    [HttpGet("today")]
    [AuthorizeRole(1)] // Admin only
    public async Task<ActionResult> GetTodaysAppointments()
    {
        try
        {
            var today = DateOnly.FromDateTime(DateTime.Today);

            var appointments = await _context.Appointments
                .Include(a => a.Pet)
                    .ThenInclude(p => p.Customer)
                .Include(a => a.Doctor)
                .Include(a => a.Service)
                .Where(a => a.AppointmentDate == today)
                .ToListAsync();

            if (appointments == null)
            {
                _logger.LogWarning("No appointments found for today: {Today}", today);
                appointments = new List<Appointment>();
            }

            var appointmentList = appointments.Select(a => new
            {
                appointmentId = a.AppointmentId,
                petId = a.PetId,
                doctorId = a.DoctorId,
                serviceId = a.ServiceId,
                appointmentDate = a.AppointmentDate.ToString("yyyy-MM-dd"),
                appointmentTime = a.AppointmentTime.ToString().Substring(0, 5), // Get HH:mm format
                weight = a.Weight,
                age = a.Age,
                isNewPet = a.IsNewPet,
                status = a.Status,
                notes = a.Notes ?? string.Empty,
                createdAt = a.CreatedAt,
                petName = a.Pet?.Name ?? "Unknown",
                customerName = a.Pet?.Customer?.CustomerName ?? "Unknown",
                doctorName = a.Doctor?.FullName ?? "Not Assigned",
                serviceName = a.Service?.Name ?? "Unknown",
                serviceDescription = a.Service?.Description ?? string.Empty,
                statusText = GetStatusText(a.Status),
                canCancel = a.Status == 0 || a.Status == 1
            })
            .OrderBy(a => a.appointmentTime)
            .ToList();

            var response = new
            {
                date = today.ToString("dd/MM/yyyy"),
                total = appointments.Count,
                pending = appointments.Count(a => a.Status == 0),
                confirmed = appointments.Count(a => a.Status == 1),
                completed = appointments.Count(a => a.Status == 2),
                cancelled = appointments.Count(a => a.Status == 3),
                appointments = appointmentList
            };

            _logger.LogInformation("Successfully retrieved {Count} appointments for today", appointments.Count);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting today's appointments");
            return StatusCode(500, new { message = "Lỗi server khi lấy danh sách lịch hẹn hôm nay", error = ex.Message });
        }
    }

    // GET: api/Dashboard/simple
    [HttpGet("simple")]
    [AuthorizeRole(1)] // Admin only
    public async Task<ActionResult<SimpleDashboardDto>> GetSimpleDashboard()
    {
        try
        {
            // Lấy thống kê lịch hẹn hôm nay
            var today = DateOnly.FromDateTime(DateTime.Today);
            
            var todayAppointments = await _context.Appointments
                .Where(a => a.AppointmentDate == today)
                .Include(a => a.Pet)
                    .ThenInclude(p => p.Customer)
                .Include(a => a.Service)
                .Include(a => a.Doctor)
                .OrderBy(a => a.AppointmentTime)
                .ToListAsync();

            var todayStats = new TodayStatsDto
            {
                TotalAppointments = todayAppointments.Count,
                PendingAppointments = todayAppointments.Count(a => a.Status == 0),
                ConfirmedAppointments = todayAppointments.Count(a => a.Status == 1),
                CompletedAppointments = todayAppointments.Count(a => a.Status == 2),
                CancelledAppointments = todayAppointments.Count(a => a.Status == 3)
            };

            // Lấy chi tiết lịch hẹn hôm nay
            var todayAppointmentDetails = todayAppointments.Select(a => new AppointmentDetailDto
            {
                AppointmentId = a.AppointmentId,
                Time = a.AppointmentTime,
                CustomerName = a.Pet?.Customer?.CustomerName ?? "Unknown",
                PetName = a.Pet?.Name ?? "Unknown",
                ServiceName = a.Service?.Name ?? "Unknown",
                DoctorName = a.Doctor?.FullName ?? "Unknown",
                Status = a.Status ?? 0,
                StatusText = GetStatusText(a.Status)
            }).ToList();

            // Lấy thống kê tỉ lệ hoàn thành và hủy (trong 30 ngày gần nhất)
            var lastMonth = DateOnly.FromDateTime(DateTime.Today.AddDays(-30));
            var recentAppointments = await _context.Appointments
                .Where(a => a.AppointmentDate >= lastMonth && a.AppointmentDate <= today)
                .ToListAsync();

            var totalRecentAppointments = recentAppointments.Count;
            var completedRecentAppointments = recentAppointments.Count(a => a.Status == 2);
            var cancelledRecentAppointments = recentAppointments.Count(a => a.Status == 3);

            var completionStats = new CompletionStatsDto
            {
                TotalAppointments = totalRecentAppointments,
                CompletedAppointments = completedRecentAppointments,
                CancelledAppointments = cancelledRecentAppointments,
                CompletionRate = totalRecentAppointments > 0 
                    ? Math.Round((double)completedRecentAppointments / totalRecentAppointments * 100, 2) 
                    : 0,
                CancellationRate = totalRecentAppointments > 0 
                    ? Math.Round((double)cancelledRecentAppointments / totalRecentAppointments * 100, 2) 
                    : 0
            };

            var result = new SimpleDashboardDto
            {
                TodayStats = todayStats,
                TodayAppointments = todayAppointmentDetails,
                CompletionStats = completionStats
            };

            _logger.LogInformation("Successfully retrieved simple dashboard data");
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting simple dashboard data");
            return StatusCode(500, new { message = "Lỗi server khi lấy dữ liệu thống kê", error = ex.Message });
        }
    }

    private async Task<List<TimeSeriesDataDto>> GetTimeSeriesData(DateTime startDate, DateTime endDate, string period)
    {
        var appointments = await _context.Appointments
            .Where(a => a.CreatedAt.HasValue && a.CreatedAt.Value >= startDate && a.CreatedAt.Value <= endDate)
            .ToListAsync();

        var groupedData = period.ToLower() switch
        {
            "day" => appointments.GroupBy(a => a.CreatedAt!.Value.Date),
            "week" => appointments.GroupBy(a => GetWeekKey(a.CreatedAt!.Value)),
            "month" => appointments.GroupBy(a => new DateTime(a.CreatedAt!.Value.Year, a.CreatedAt!.Value.Month, 1)),
            "quarter" => appointments.GroupBy(a => GetQuarterKey(a.CreatedAt!.Value)),
            "year" => appointments.GroupBy(a => new DateTime(a.CreatedAt!.Value.Year, 1, 1)),
            _ => appointments.GroupBy(a => new DateTime(a.CreatedAt!.Value.Year, a.CreatedAt!.Value.Month, 1))
        };

        return groupedData.Select(g => new TimeSeriesDataDto
        {
            Date = g.Key.ToString("yyyy-MM-dd"),
            TotalAppointments = g.Count(),
            CompletedAppointments = g.Count(a => a.Status == 2),
            CancelledAppointments = g.Count(a => a.Status == 3),
            CompletionRate = g.Count() > 0 ? (double)g.Count(a => a.Status == 2) / g.Count() * 100 : 0
        }).OrderBy(t => t.Date).ToList();
    }

    private async Task<decimal> CalculateRevenue(DateTime startDate, DateTime endDate)
    {
        var completedAppointments = await _context.Appointments
            .Where(a => a.CreatedAt.HasValue && a.CreatedAt.Value >= startDate && a.CreatedAt.Value <= endDate && a.Status == 2)
            .Include(a => a.Service)
            .ToListAsync();

        return completedAppointments
            .Where(a => a.Service != null && a.Service.Price.HasValue)
            .Sum(a => a.Service!.Price!.Value);
    }

    private static DateTime GetWeekKey(DateTime date)
    {
        var diff = date.DayOfWeek - DayOfWeek.Monday;
        if (diff < 0) diff += 7;
        return date.AddDays(-diff).Date;
    }

    private static DateTime GetQuarterKey(DateTime date)
    {
        var quarter = (date.Month - 1) / 3 + 1;
        return new DateTime(date.Year, (quarter - 1) * 3 + 1, 1);
    }

    private static string GetStatusText(int? status)
    {
        return status switch
        {
            0 => "Chờ xác nhận",
            1 => "Đã xác nhận", 
            2 => "Hoàn thành",
            3 => "Đã hủy",
            _ => "Không xác định"
        };
    }
} 