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

            // Get appointments data - Filter by CreatedAt (ngày tạo lịch)
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

            // Current period data - Filter by CreatedAt (ngày tạo lịch)
            var currentAppointments = await _context.Appointments
                .Where(a => a.CreatedAt.HasValue && a.CreatedAt.Value >= startDate && a.CreatedAt.Value <= endDate)
                .ToListAsync();

            // Previous period data - Filter by CreatedAt (ngày tạo lịch)
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

    // GET: api/Dashboard/flexible
    [HttpGet("flexible")]
    [AuthorizeRole(1)] // Admin only
    public async Task<ActionResult<SimpleDashboardDto>> GetFlexibleDashboard(
        [FromQuery] string filterType = "today", // today, specific-date, last-7-days, last-30-days, this-week, last-week, specific-month
        [FromQuery] string? specificDate = null, // Format: yyyy-MM-dd
        [FromQuery] int? month = null, // 1-12
        [FromQuery] int? year = null) // yyyy
    {
        try
        {
            DateTime startDate;
            DateTime endDate;
            string displayPeriod;

            switch (filterType.ToLower())
            {
                case "specific-date":
                    if (string.IsNullOrEmpty(specificDate) || !DateTime.TryParse(specificDate, out var parsedDate))
                    {
                        return BadRequest(new { message = "Ngày không hợp lệ. Sử dụng định dạng yyyy-MM-dd" });
                    }
                    startDate = parsedDate.Date;
                    endDate = parsedDate.Date.AddDays(1).AddSeconds(-1);
                    displayPeriod = $"Ngày {parsedDate:dd/MM/yyyy}";
                    break;

                case "last-7-days":
                    endDate = DateTime.Today.AddDays(1).AddSeconds(-1);
                    startDate = DateTime.Today.AddDays(-6); // 7 ngày bao gồm hôm nay
                    displayPeriod = "7 ngày gần nhất";
                    break;

                case "last-30-days":
                    endDate = DateTime.Today.AddDays(1).AddSeconds(-1);
                    startDate = DateTime.Today.AddDays(-29); // 30 ngày bao gồm hôm nay
                    displayPeriod = "30 ngày gần nhất";
                    break;

                case "this-week":
                    var thisWeekStart = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek + (int)DayOfWeek.Monday);
                    if (DateTime.Today.DayOfWeek == DayOfWeek.Sunday)
                        thisWeekStart = thisWeekStart.AddDays(-7);
                    startDate = thisWeekStart;
                    endDate = thisWeekStart.AddDays(7).AddSeconds(-1);
                    displayPeriod = $"Tuần này ({thisWeekStart:dd/MM} - {thisWeekStart.AddDays(6):dd/MM/yyyy})";
                    break;

                case "last-week":
                    var lastWeekStart = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek + (int)DayOfWeek.Monday - 7);
                    if (DateTime.Today.DayOfWeek == DayOfWeek.Sunday)
                        lastWeekStart = lastWeekStart.AddDays(-7);
                    startDate = lastWeekStart;
                    endDate = lastWeekStart.AddDays(7).AddSeconds(-1);
                    displayPeriod = $"Tuần trước ({lastWeekStart:dd/MM} - {lastWeekStart.AddDays(6):dd/MM/yyyy})";
                    break;

                case "specific-month":
                    if (!month.HasValue || month < 1 || month > 12)
                    {
                        return BadRequest(new { message = "Tháng phải từ 1-12" });
                    }
                    var targetYear = year ?? DateTime.Today.Year;
                    startDate = new DateTime(targetYear, month.Value, 1);
                    endDate = startDate.AddMonths(1).AddSeconds(-1);
                    displayPeriod = $"Tháng {month}/{targetYear}";
                    break;

                case "today":
                default:
                    startDate = DateTime.Today;
                    endDate = DateTime.Today.AddDays(1).AddSeconds(-1);
                    displayPeriod = "Hôm nay";
                    break;
            }

            // Lấy lịch hẹn TẠO trong khoảng thời gian (theo CreatedAt)
            var appointments = await _context.Appointments
                .Where(a => a.CreatedAt.HasValue && a.CreatedAt.Value >= startDate && a.CreatedAt.Value <= endDate)
                .Include(a => a.Pet)
                    .ThenInclude(p => p.Customer)
                .Include(a => a.Service)
                .Include(a => a.Doctor)
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();

            var stats = new TodayStatsDto
            {
                TotalAppointments = appointments.Count,
                PendingAppointments = appointments.Count(a => a.Status == 0),
                ConfirmedAppointments = appointments.Count(a => a.Status == 1),
                CompletedAppointments = appointments.Count(a => a.Status == 2),
                CancelledAppointments = appointments.Count(a => a.Status == 3)
            };

            var appointmentDetails = appointments.Select(a => new AppointmentDetailDto
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

            // Lấy thống kê tỉ lệ hoàn thành THEO FILTER ĐÃ CHỌN (sử dụng cùng startDate/endDate)
            // Sử dụng appointments đã lấy ở trên để tính completion stats
            var totalRecent = appointments.Count;
            var completedRecent = appointments.Count(a => a.Status == 2);
            var cancelledRecent = appointments.Count(a => a.Status == 3);
            var pendingRecent = appointments.Count(a => a.Status == 0);
            var confirmedRecent = appointments.Count(a => a.Status == 1);

            var completionStats = new CompletionStatsDto
            {
                TotalAppointments = totalRecent,
                CompletedAppointments = completedRecent,
                CancelledAppointments = cancelledRecent,
                PendingAppointments = pendingRecent,
                ConfirmedAppointments = confirmedRecent,
                CompletionRate = totalRecent > 0 ? Math.Round((double)completedRecent / totalRecent * 100, 2) : 0,
                CancellationRate = totalRecent > 0 ? Math.Round((double)cancelledRecent / totalRecent * 100, 2) : 0,
                PendingRate = totalRecent > 0 ? Math.Round((double)pendingRecent / totalRecent * 100, 2) : 0,
                ConfirmedRate = totalRecent > 0 ? Math.Round((double)confirmedRecent / totalRecent * 100, 2) : 0
            };

            // Tính toán doanh thu
            var totalRevenue = await CalculateRevenue(startDate, endDate);
            var averageRevenue = completedRecent > 0 ? totalRevenue / completedRecent : 0;

            // Tính tăng trưởng doanh thu (so với kỳ trước) - logic cải tiến
            DateTime previousStartDate;
            DateTime previousEndDate;
            string currentPeriodLabel;
            string previousPeriodLabel;
            
            var periodDays = (int)(endDate - startDate).TotalDays + 1;
            
            switch (filterType)
            {
                case "today":
                    // Hôm nay so với hôm qua
                    previousStartDate = startDate.AddDays(-1);
                    previousEndDate = endDate.AddDays(-1);
                    currentPeriodLabel = "Hôm nay";
                    previousPeriodLabel = "Hôm qua";
                    break;
                    
                case "last-7-days":
                    // 7 ngày gần nhất so với 7 ngày trước đó
                    previousStartDate = startDate.AddDays(-7);
                    previousEndDate = startDate.AddSeconds(-1);
                    currentPeriodLabel = "7 ngày gần nhất";
                    previousPeriodLabel = "7 ngày trước đó";
                    break;
                    
                case "last-30-days":
                    // 30 ngày gần nhất so với 30 ngày trước đó
                    previousStartDate = startDate.AddDays(-30);
                    previousEndDate = startDate.AddSeconds(-1);
                    currentPeriodLabel = "30 ngày gần nhất";
                    previousPeriodLabel = "30 ngày trước đó";
                    break;
                    
                case "this-week":
                    // Tuần này so với tuần trước
                    previousStartDate = startDate.AddDays(-7);
                    previousEndDate = endDate.AddDays(-7);
                    currentPeriodLabel = "Tuần này";
                    previousPeriodLabel = "Tuần trước";
                    break;
                    
                case "last-week":
                    // Tuần trước so với tuần trước nữa
                    previousStartDate = startDate.AddDays(-7);
                    previousEndDate = endDate.AddDays(-7);
                    currentPeriodLabel = "Tuần trước";
                    previousPeriodLabel = "2 tuần trước";
                    break;
                    
                case "specific-month":
                    // Tháng được chọn so với tháng trước đó
                    previousStartDate = startDate.AddMonths(-1);
                    previousEndDate = endDate.AddMonths(-1);
                    currentPeriodLabel = $"Tháng {month}/{year ?? DateTime.Today.Year}";
                    var prevMonth = startDate.AddMonths(-1);
                    previousPeriodLabel = $"Tháng {prevMonth.Month}/{prevMonth.Year}";
                    break;
                    
                case "specific-date":
                    // Ngày cụ thể so với ngày hôm trước
                    previousStartDate = startDate.AddDays(-1);
                    previousEndDate = endDate.AddDays(-1);
                    currentPeriodLabel = startDate.ToString("dd/MM/yyyy");
                    previousPeriodLabel = previousStartDate.ToString("dd/MM/yyyy");
                    break;
                    
                default:
                    // Mặc định: so với kỳ trước cùng độ dài
                    previousStartDate = startDate.AddDays(-periodDays);
                    previousEndDate = startDate.AddSeconds(-1);
                    currentPeriodLabel = "Kỳ hiện tại";
                    previousPeriodLabel = "Kỳ trước";
                    break;
            }
            
            var previousRevenue = await CalculateRevenue(previousStartDate, previousEndDate);
            var revenueDifference = totalRevenue - previousRevenue;
            var revenueGrowth = previousRevenue > 0 
                ? Math.Round((double)(revenueDifference / previousRevenue * 100), 2) 
                : (totalRevenue > 0 ? 100 : 0);

            var revenueStats = new RevenueStatsDto
            {
                TotalRevenue = totalRevenue,
                AverageRevenue = Math.Round(averageRevenue, 2),
                RevenueGrowth = (decimal)revenueGrowth,
                Comparison = new RevenueComparisonDto
                {
                    CurrentPeriodRevenue = totalRevenue,
                    PreviousPeriodRevenue = previousRevenue,
                    RevenueDifference = revenueDifference,
                    GrowthPercentage = (decimal)revenueGrowth,
                    CurrentPeriodLabel = currentPeriodLabel,
                    PreviousPeriodLabel = previousPeriodLabel
                }
            };

            var result = new SimpleDashboardDto
            {
                TodayStats = stats,
                TodayAppointments = appointmentDetails,
                CompletionStats = completionStats,
                RevenueStats = revenueStats
            };

            _logger.LogInformation($"Successfully retrieved flexible dashboard data for period: {displayPeriod}");
            return Ok(new { period = displayPeriod, data = result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting flexible dashboard data");
            return StatusCode(500, new { message = "Lỗi server khi lấy dữ liệu thống kê", error = ex.Message });
        }
    }

    // GET: api/Dashboard/simple
    [HttpGet("simple")]
    [AuthorizeRole(1)] // Admin only
    public async Task<ActionResult<SimpleDashboardDto>> GetSimpleDashboard()
    {
        try
        {
            // Lấy thống kê lịch hẹn TẠO hôm nay (theo CreatedAt)
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            var todayAppointments = await _context.Appointments
                .Where(a => a.CreatedAt.HasValue && a.CreatedAt.Value >= today && a.CreatedAt.Value < tomorrow)
                .Include(a => a.Pet)
                    .ThenInclude(p => p.Customer)
                .Include(a => a.Service)
                .Include(a => a.Doctor)
                .OrderByDescending(a => a.CreatedAt)
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

            // Lấy thống kê tỉ lệ hoàn thành (30 ngày gần nhất)
            var last30Days = DateTime.Today.AddDays(-30);
            var recentAppointments = await _context.Appointments
                .Where(a => a.CreatedAt.HasValue && a.CreatedAt.Value >= last30Days)
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
        // Filter by CreatedAt (ngày tạo lịch)
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

    // GET: api/Dashboard/monthly-revenue
    [HttpGet("monthly-revenue")]
    [AuthorizeRole(1)] // Admin only
    public async Task<ActionResult<YearlyRevenueStatsDto>> GetMonthlyRevenue([FromQuery] int? year = null)
    {
        try
        {
            var targetYear = year ?? DateTime.Today.Year;
            
            var monthlyData = new List<MonthlyRevenueDto>();
            var monthNames = new[] { "", "Tháng 1", "Tháng 2", "Tháng 3", "Tháng 4", "Tháng 5", "Tháng 6", 
                                     "Tháng 7", "Tháng 8", "Tháng 9", "Tháng 10", "Tháng 11", "Tháng 12" };
            
            for (int month = 1; month <= 12; month++)
            {
                // Tính doanh thu tháng hiện tại
                var startDate = new DateTime(targetYear, month, 1);
                var endDate = startDate.AddMonths(1).AddSeconds(-1);
                var currentRevenue = await CalculateRevenue(startDate, endDate);
                
                // Tính doanh thu tháng cùng kỳ năm trước
                var previousYearStart = startDate.AddYears(-1);
                var previousYearEnd = endDate.AddYears(-1);
                var previousYearRevenue = await CalculateRevenue(previousYearStart, previousYearEnd);
                
                // Tính tăng trưởng so với cùng kỳ năm trước
                var growthPercentage = previousYearRevenue > 0 
                    ? Math.Round((double)((currentRevenue - previousYearRevenue) / previousYearRevenue * 100), 2)
                    : (currentRevenue > 0 ? 100 : 0);
                
                monthlyData.Add(new MonthlyRevenueDto
                {
                    Month = month,
                    MonthName = monthNames[month],
                    Revenue = currentRevenue,
                    GrowthPercentage = (decimal)growthPercentage
                });
            }
            
            var totalRevenue = monthlyData.Sum(m => m.Revenue);
            var averageMonthlyRevenue = Math.Round(totalRevenue / 12, 2);
            
            var result = new YearlyRevenueStatsDto
            {
                Year = targetYear,
                MonthlyData = monthlyData,
                TotalRevenue = totalRevenue,
                AverageMonthlyRevenue = averageMonthlyRevenue
            };
            
            _logger.LogInformation($"Successfully retrieved monthly revenue for year {targetYear}");
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting monthly revenue");
            return StatusCode(500, new { message = "Lỗi server khi lấy dữ liệu doanh thu theo tháng", error = ex.Message });
        }
    }

    private async Task<decimal> CalculateRevenue(DateTime startDate, DateTime endDate)
    {
        // Calculate revenue by CreatedAt (ngày tạo lịch)
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
        // GET: api/Dashboard/service-stats
    [HttpGet("service-stats")]
    [AuthorizeRole(1)] // Admin only
    public async Task<ActionResult<ServiceStatsDto>> GetServiceStats(
        [FromQuery] string filterType = "today",
        [FromQuery] string? specificDate = null,
        [FromQuery] int? month = null,
        [FromQuery] int? year = null)
    {
        try
        {
            DateTime startDate;
            DateTime endDate;
            
            // Xác định khoảng thời gian (giống như flexible endpoint)
            switch (filterType.ToLower())
            {
                case "specific-date":
                    if (string.IsNullOrEmpty(specificDate) || !DateTime.TryParse(specificDate, out var parsedDate))
                    {
                        return BadRequest(new { message = "Ngày không hợp lệ" });
                    }
                    startDate = parsedDate.Date;
                    endDate = parsedDate.Date.AddDays(1).AddSeconds(-1);
                    break;

                case "last-7-days":
                    endDate = DateTime.Today.AddDays(1).AddSeconds(-1);
                    startDate = DateTime.Today.AddDays(-6);
                    break;

                case "last-30-days":
                    endDate = DateTime.Today.AddDays(1).AddSeconds(-1);
                    startDate = DateTime.Today.AddDays(-29);
                    break;

                case "this-week":
                    var thisWeekStart = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek + (int)DayOfWeek.Monday);
                    if (DateTime.Today.DayOfWeek == DayOfWeek.Sunday)
                        thisWeekStart = thisWeekStart.AddDays(-7);
                    startDate = thisWeekStart;
                    endDate = thisWeekStart.AddDays(7).AddSeconds(-1);
                    break;

                case "last-week":
                    var lastWeekStart = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek + (int)DayOfWeek.Monday - 7);
                    if (DateTime.Today.DayOfWeek == DayOfWeek.Sunday)
                        lastWeekStart = lastWeekStart.AddDays(-7);
                    startDate = lastWeekStart;
                    endDate = lastWeekStart.AddDays(7).AddSeconds(-1);
                    break;

                case "specific-month":
                    if (!month.HasValue || month < 1 || month > 12)
                    {
                        return BadRequest(new { message = "Tháng phải từ 1-12" });
                    }
                    var targetYear = year ?? DateTime.Today.Year;
                    startDate = new DateTime(targetYear, month.Value, 1);
                    endDate = startDate.AddMonths(1).AddSeconds(-1);
                    break;

                case "today":
                default:
                    startDate = DateTime.Today;
                    endDate = DateTime.Today.AddDays(1).AddSeconds(-1);
                    break;
            }

            // Lấy tất cả dịch vụ
            var allServices = await _context.Services.ToListAsync();
            var totalServices = allServices.Count;

            // Lấy appointments trong khoảng thời gian
            var appointments = await _context.Appointments
                .Where(a => a.CreatedAt.HasValue && a.CreatedAt.Value >= startDate && a.CreatedAt.Value <= endDate)
                .Include(a => a.Service)
                .ToListAsync();

            // Thống kê theo dịch vụ
            var serviceStats = appointments
                .Where(a => a.Service != null && a.ServiceId > 0)
                .GroupBy(a => new { a.ServiceId, a.Service })
                .Select(g => new ServiceDetailStatsDto
                {
                    ServiceId = g.Key.ServiceId,
                    ServiceName = g.Key.Service!.Name ?? "Unknown",
                    Description = g.Key.Service.Description,
                    Price = g.Key.Service.Price,
                    TotalAppointments = g.Count(),
                    CompletedAppointments = g.Count(a => a.Status == 2),
                    CancelledAppointments = g.Count(a => a.Status == 3),
                    CompletionRate = g.Count() > 0 ? Math.Round((double)g.Count(a => a.Status == 2) / g.Count() * 100, 2) : 0,
                    TotalRevenue = g.Where(a => a.Status == 2 && g.Key.Service.Price.HasValue)
                                    .Sum(a => g.Key.Service.Price!.Value)
                })
                .OrderByDescending(s => s.TotalAppointments)
                .ToList();

            // Top 10 dịch vụ phổ biến nhất
            var topServices = serviceStats.Take(10).ToList();

            // Doanh thu theo dịch vụ
            var totalRevenue = serviceStats.Sum(s => s.TotalRevenue);
            var revenueByService = serviceStats
                .Where(s => s.TotalRevenue > 0)
                .Select(s => new ServiceRevenueDto
                {
                    ServiceId = s.ServiceId,
                    ServiceName = s.ServiceName,
                    Revenue = s.TotalRevenue,
                    CompletedCount = s.CompletedAppointments,
                    PercentageOfTotal = totalRevenue > 0 ? Math.Round((double)(s.TotalRevenue / totalRevenue * 100), 2) : 0
                })
                .OrderByDescending(s => s.Revenue)
                .ToList();

            // Giá trung bình của dịch vụ
            var averageServicePrice = allServices.Where(s => s.Price.HasValue).Average(s => s.Price!.Value);

            var result = new ServiceStatsDto
            {
                TotalServices = totalServices,
                TopServices = topServices,
                RevenueByService = revenueByService,
                TotalRevenue = totalRevenue,
                AverageServicePrice = Math.Round(averageServicePrice, 2)
            };

            _logger.LogInformation("Successfully retrieved service statistics");
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting service statistics");
            return StatusCode(500, new { message = "Lỗi server khi lấy thống kê dịch vụ", error = ex.Message });
        }
    }

    // GET: api/Dashboard/customer-stats
    [HttpGet("customer-stats")]
    [AuthorizeRole(1)] // Admin only
    public async Task<ActionResult<CustomerStatsDto>> GetCustomerStats(
        [FromQuery] string filterType = "today",
        [FromQuery] string? specificDate = null,
        [FromQuery] int? month = null,
        [FromQuery] int? year = null)
    {
        try
        {
            DateTime startDate;
            DateTime endDate;
            
            // Xác định khoảng thời gian
            switch (filterType.ToLower())
            {
                case "specific-date":
                    if (string.IsNullOrEmpty(specificDate) || !DateTime.TryParse(specificDate, out var parsedDate))
                    {
                        return BadRequest(new { message = "Ngày không hợp lệ" });
                    }
                    startDate = parsedDate.Date;
                    endDate = parsedDate.Date.AddDays(1).AddSeconds(-1);
                    break;

                case "last-7-days":
                    endDate = DateTime.Today.AddDays(1).AddSeconds(-1);
                    startDate = DateTime.Today.AddDays(-6);
                    break;

                case "last-30-days":
                    endDate = DateTime.Today.AddDays(1).AddSeconds(-1);
                    startDate = DateTime.Today.AddDays(-29);
                    break;

                case "this-week":
                    var thisWeekStart = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek + (int)DayOfWeek.Monday);
                    if (DateTime.Today.DayOfWeek == DayOfWeek.Sunday)
                        thisWeekStart = thisWeekStart.AddDays(-7);
                    startDate = thisWeekStart;
                    endDate = thisWeekStart.AddDays(7).AddSeconds(-1);
                    break;

                case "last-week":
                    var lastWeekStart = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek + (int)DayOfWeek.Monday - 7);
                    if (DateTime.Today.DayOfWeek == DayOfWeek.Sunday)
                        lastWeekStart = lastWeekStart.AddDays(-7);
                    startDate = lastWeekStart;
                    endDate = lastWeekStart.AddDays(7).AddSeconds(-1);
                    break;

                case "specific-month":
                    if (!month.HasValue || month < 1 || month > 12)
                    {
                        return BadRequest(new { message = "Tháng phải từ 1-12" });
                    }
                    var targetYear = year ?? DateTime.Today.Year;
                    startDate = new DateTime(targetYear, month.Value, 1);
                    endDate = startDate.AddMonths(1).AddSeconds(-1);
                    break;

                case "today":
                default:
                    startDate = DateTime.Today;
                    endDate = DateTime.Today.AddDays(1).AddSeconds(-1);
                    break;
            }

            // Tổng số khách hàng
            var totalCustomers = await _context.Customers
                .Include(c => c.User)
                .CountAsync();

            // Khách hàng mới trong kỳ
            var newCustomers = await _context.Customers
                .Include(c => c.User)
                .Where(c => c.User != null && c.User.CreatedAt >= startDate && c.User.CreatedAt <= endDate)
                .CountAsync();

            // Khách hàng quay lại (có nhiều hơn 1 lịch hẹn)
            var returningCustomers = await _context.Customers
                .Include(c => c.Pets)
                    .ThenInclude(p => p.Appointments)
                .Where(c => c.Pets.Any(p => p.Appointments.Count > 1))
                .CountAsync();

            // Tính tăng trưởng khách hàng
            var previousStartDate = startDate.AddDays(-(endDate - startDate).Days - 1);
            var previousEndDate = startDate.AddDays(-1);
            var previousNewCustomers = await _context.Customers
                .Include(c => c.User)
                .Where(c => c.User != null && c.User.CreatedAt >= previousStartDate && c.User.CreatedAt <= previousEndDate)
                .CountAsync();
            
            var customerGrowth = previousNewCustomers > 0 
                ? Math.Round((double)(newCustomers - previousNewCustomers) / previousNewCustomers * 100, 2) 
                : 0;

            // Trung bình lịch hẹn mỗi khách hàng
            var totalAppointments = await _context.Appointments
                .Where(a => a.CreatedAt.HasValue && a.CreatedAt.Value >= startDate && a.CreatedAt.Value <= endDate)
                .CountAsync();
            var averageAppointmentsPerCustomer = totalCustomers > 0 
                ? Math.Round((double)totalAppointments / totalCustomers, 2) 
                : 0;

            // Trung bình thú cưng mỗi khách hàng
            var totalPets = await _context.Pets.CountAsync();
            var averagePetsPerCustomer = totalCustomers > 0 
                ? Math.Round((double)totalPets / totalCustomers, 2) 
                : 0;

            // Top khách hàng
            var topCustomers = await _context.Customers
                .Include(c => c.User)
                .Include(c => c.Pets)
                    .ThenInclude(p => p.Appointments.Where(a => a.CreatedAt.HasValue && a.CreatedAt.Value >= startDate && a.CreatedAt.Value <= endDate))
                        .ThenInclude(a => a.Service)
                .Select(c => new
                {
                    Customer = c,
                    AppointmentCount = c.Pets.SelectMany(p => p.Appointments).Count(),
                    TotalSpent = c.Pets.SelectMany(p => p.Appointments)
                        .Where(a => a.Status == 2 && a.Service != null && a.Service.Price.HasValue)
                        .Sum(a => a.Service!.Price!.Value),
                    LastVisit = c.Pets.SelectMany(p => p.Appointments)
                        .OrderByDescending(a => a.CreatedAt)
                        .Select(a => a.CreatedAt)
                        .FirstOrDefault()
                })
                .Where(x => x.AppointmentCount > 0)
                .OrderByDescending(x => x.AppointmentCount)
                .Take(10)
                .ToListAsync();

            var topCustomerList = topCustomers.Select(x => new TopCustomerDto
            {
                CustomerId = x.Customer.CustomerId,
                CustomerName = x.Customer.CustomerName ?? "Unknown",
                Email = x.Customer.User?.Email,
                PhoneNumber = x.Customer.User?.PhoneNumber,
                TotalAppointments = x.AppointmentCount,
                TotalPets = x.Customer.Pets.Count,
                TotalSpent = x.TotalSpent,
                LastVisit = x.LastVisit
            }).ToList();

            // Phân loại khách hàng theo mức độ hoạt động
            var allCustomersActivity = await _context.Customers
                .Include(c => c.Pets)
                    .ThenInclude(p => p.Appointments)
                .Select(c => new
                {
                    Customer = c,
                    AppointmentCount = c.Pets.SelectMany(p => p.Appointments)
                        .Count(a => a.CreatedAt.HasValue && a.CreatedAt.Value >= startDate && a.CreatedAt.Value <= endDate)
                })
                .ToListAsync();

            var highActivity = allCustomersActivity.Count(x => x.AppointmentCount >= 5);
            var mediumActivity = allCustomersActivity.Count(x => x.AppointmentCount >= 2 && x.AppointmentCount < 5);
            var lowActivity = allCustomersActivity.Count(x => x.AppointmentCount >= 1 && x.AppointmentCount < 2);

            var customersByActivity = new List<CustomerActivityDto>
            {
                new CustomerActivityDto 
                { 
                    ActivityLevel = "Cao (≥5 lịch hẹn)", 
                    CustomerCount = highActivity,
                    Percentage = totalCustomers > 0 ? Math.Round((double)highActivity / totalCustomers * 100, 2) : 0
                },
                new CustomerActivityDto 
                { 
                    ActivityLevel = "Trung bình (2-4 lịch hẹn)", 
                    CustomerCount = mediumActivity,
                    Percentage = totalCustomers > 0 ? Math.Round((double)mediumActivity / totalCustomers * 100, 2) : 0
                },
                new CustomerActivityDto 
                { 
                    ActivityLevel = "Thấp (1 lịch hẹn)", 
                    CustomerCount = lowActivity,
                    Percentage = totalCustomers > 0 ? Math.Round((double)lowActivity / totalCustomers * 100, 2) : 0
                }
            };

            var result = new CustomerStatsDto
            {
                TotalCustomers = totalCustomers,
                NewCustomers = newCustomers,
                ReturningCustomers = returningCustomers,
                CustomerGrowth = customerGrowth,
                AverageAppointmentsPerCustomer = averageAppointmentsPerCustomer,
                AveragePetsPerCustomer = averagePetsPerCustomer,
                TopCustomers = topCustomerList,
                CustomersByActivity = customersByActivity
            };

            _logger.LogInformation("Successfully retrieved customer statistics");
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting customer statistics");
            return StatusCode(500, new { message = "Lỗi server khi lấy thống kê khách hàng", error = ex.Message });
        }
    }
} 