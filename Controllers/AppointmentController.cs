using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using ThuYBinhDuongAPI.Data.Dtos;
using ThuYBinhDuongAPI.Models;
using ThuYBinhDuongAPI.Services;

namespace ThuYBinhDuongAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // Yêu cầu đăng nhập cho tất cả endpoints
    public class AppointmentController : ControllerBase
    {
        private readonly ThuybinhduongContext _context;
        private readonly IJwtService _jwtService;
        private readonly IEmailService _emailService;
        private readonly ILogger<AppointmentController> _logger;

        public AppointmentController(ThuybinhduongContext context, IJwtService jwtService, IEmailService emailService, ILogger<AppointmentController> logger)
        {
            _context = context;
            _jwtService = jwtService;
            _emailService = emailService;
            _logger = logger;
        }

        /// <summary>
        /// Lấy danh sách lịch hẹn của khách hàng hiện tại
        /// </summary>
        [HttpGet]
        [AuthorizeRole(0)] // Chỉ khách hàng
        public async Task<ActionResult<IEnumerable<AppointmentResponseDto>>> GetMyAppointments()
        {
            try
            {
                var customerId = await GetCurrentCustomerIdAsync();
                if (customerId == null)
                {
                    return BadRequest(new { message = "Không tìm thấy thông tin khách hàng" });
                }

                var appointments = await _context.Appointments
                    .Include(a => a.Pet)
                        .ThenInclude(p => p.Customer)
                    .Include(a => a.Doctor)
                    .Include(a => a.Service)
                    .Where(a => a.Pet.CustomerId == customerId.Value)
                    .Select(a => new AppointmentResponseDto
                    {
                        AppointmentId = a.AppointmentId,
                        PetId = a.PetId,
                        DoctorId = a.DoctorId,
                        ServiceId = a.ServiceId,
                        AppointmentDate = a.AppointmentDate,
                        AppointmentTime = a.AppointmentTime,
                        Weight = a.Weight,
                        Age = a.Age,
                        IsNewPet = a.IsNewPet,
                        Status = a.Status,
                        Notes = a.Notes,
                        CreatedAt = a.CreatedAt,
                        PetName = a.Pet.Name,
                        CustomerName = a.Pet.Customer.CustomerName,
                        DoctorName = a.Doctor != null ? a.Doctor.FullName : null,
                        ServiceName = a.Service.Name,
                        ServiceDescription = a.Service.Description,
                        StatusText = GetStatusText(a.Status),
                        CanCancel = a.Status == 0 // Chỉ có thể hủy khi status = 0 (chờ xác nhận)
                    })
                    .OrderByDescending(a => a.CreatedAt)
                    .ToListAsync();

                _logger.LogInformation($"Customer {customerId} retrieved {appointments.Count} appointments");
                return Ok(appointments);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving appointments");
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi lấy danh sách lịch hẹn" });
            }
        }

        /// <summary>
        /// Lấy thông tin chi tiết một lịch hẹn
        /// </summary>
        [HttpGet("{id}")]
        [AuthorizeRole(0)] // Chỉ khách hàng
        public async Task<ActionResult<AppointmentResponseDto>> GetAppointment(int id)
        {
            try
            {
                var customerId = await GetCurrentCustomerIdAsync();
                if (customerId == null)
                {
                    return BadRequest(new { message = "Không tìm thấy thông tin khách hàng" });
                }

                var appointment = await _context.Appointments
                    .Include(a => a.Pet)
                        .ThenInclude(p => p.Customer)
                    .Include(a => a.Doctor)
                    .Include(a => a.Service)
                    .Where(a => a.AppointmentId == id && a.Pet.CustomerId == customerId.Value)
                    .Select(a => new AppointmentResponseDto
                    {
                        AppointmentId = a.AppointmentId,
                        PetId = a.PetId,
                        DoctorId = a.DoctorId,
                        ServiceId = a.ServiceId,
                        AppointmentDate = a.AppointmentDate,
                        AppointmentTime = a.AppointmentTime,
                        Weight = a.Weight,
                        Age = a.Age,
                        IsNewPet = a.IsNewPet,
                        Status = a.Status,
                        Notes = a.Notes,
                        CreatedAt = a.CreatedAt,
                        PetName = a.Pet.Name,
                        CustomerName = a.Pet.Customer.CustomerName,
                        DoctorName = a.Doctor != null ? a.Doctor.FullName : null,
                        ServiceName = a.Service.Name,
                        ServiceDescription = a.Service.Description,
                        StatusText = GetStatusText(a.Status),
                        CanCancel = a.Status == 0
                    })
                    .FirstOrDefaultAsync();

                if (appointment == null)
                {
                    return NotFound(new { message = "Không tìm thấy lịch hẹn hoặc bạn không có quyền truy cập" });
                }

                _logger.LogInformation($"Customer {customerId} retrieved appointment {id}");
                return Ok(appointment);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving appointment {AppointmentId}", id);
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi lấy thông tin lịch hẹn" });
            }
        }

        /// <summary>
        /// Đặt lịch hẹn mới
        /// </summary>
        [HttpPost]
        [AuthorizeRole(0)] // Chỉ khách hàng
        public async Task<ActionResult<AppointmentResponseDto>> CreateAppointment([FromBody] CreateAppointmentDto createDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var customerId = await GetCurrentCustomerIdAsync();
                if (customerId == null)
                {
                    return BadRequest(new { message = "Không tìm thấy thông tin khách hàng" });
                }

                // Kiểm tra thú cưng có thuộc về khách hàng không
                var pet = await _context.Pets
                    .FirstOrDefaultAsync(p => p.PetId == createDto.PetId && p.CustomerId == customerId.Value);

                if (pet == null)
                {
                    return BadRequest(new { message = "Thú cưng không tồn tại hoặc không thuộc về bạn" });
                }

                // Kiểm tra dịch vụ có tồn tại không
                var service = await _context.Services.FindAsync(createDto.ServiceId);
                if (service == null)
                {
                    return BadRequest(new { message = "Dịch vụ không tồn tại" });
                }

                // Kiểm tra bác sĩ (nếu có chỉ định)
                if (createDto.DoctorId.HasValue)
                {
                    var doctor = await _context.Doctors.FindAsync(createDto.DoctorId.Value);
                    if (doctor == null)
                    {
                        return BadRequest(new { message = "Bác sĩ không tồn tại" });
                    }
                }

                // Kiểm tra ngày hẹn không được trong quá khứ
                if (createDto.AppointmentDate < DateOnly.FromDateTime(DateTime.Today))
                {
                    return BadRequest(new { message = "Ngày hẹn không thể trong quá khứ" });
                }

                // Kiểm tra trùng lịch cho cùng thú cưng trong cùng ngày và giờ
                var existingAppointment = await _context.Appointments
                    .AnyAsync(a => a.PetId == createDto.PetId && 
                                  a.AppointmentDate == createDto.AppointmentDate && 
                                  a.AppointmentTime == createDto.AppointmentTime &&
                                  (a.Status == 0 || a.Status == 1)); // Chờ xác nhận hoặc đã xác nhận

                if (existingAppointment)
                {
                    return BadRequest(new { message = "Thú cưng đã có lịch hẹn vào thời gian này" });
                }

                var appointment = new Appointment
                {
                    PetId = createDto.PetId,
                    ServiceId = createDto.ServiceId,
                    DoctorId = createDto.DoctorId,
                    AppointmentDate = createDto.AppointmentDate,
                    AppointmentTime = createDto.AppointmentTime,
                    Weight = createDto.Weight,
                    Age = createDto.Age,
                    IsNewPet = createDto.IsNewPet,
                    Status = 0, // Chờ xác nhận
                    Notes = createDto.Notes,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Appointments.Add(appointment);
                await _context.SaveChangesAsync();

                // Load thông tin liên quan để trả về
                await _context.Entry(appointment)
                    .Reference(a => a.Pet)
                    .LoadAsync();
                await _context.Entry(appointment.Pet)
                    .Reference(p => p.Customer)
                    .LoadAsync();
                await _context.Entry(appointment)
                    .Reference(a => a.Service)
                    .LoadAsync();
                if (appointment.DoctorId.HasValue)
                {
                    await _context.Entry(appointment)
                        .Reference(a => a.Doctor)
                        .LoadAsync();
                }

                var response = new AppointmentResponseDto
                {
                    AppointmentId = appointment.AppointmentId,
                    PetId = appointment.PetId,
                    DoctorId = appointment.DoctorId,
                    ServiceId = appointment.ServiceId,
                    AppointmentDate = appointment.AppointmentDate,
                    AppointmentTime = appointment.AppointmentTime,
                    Weight = appointment.Weight,
                    Age = appointment.Age,
                    IsNewPet = appointment.IsNewPet,
                    Status = appointment.Status,
                    Notes = appointment.Notes,
                    CreatedAt = appointment.CreatedAt,
                    PetName = appointment.Pet.Name,
                    CustomerName = appointment.Pet.Customer.CustomerName,
                    DoctorName = appointment.Doctor?.FullName,
                    ServiceName = appointment.Service.Name,
                    ServiceDescription = appointment.Service.Description,
                    StatusText = GetStatusText(appointment.Status),
                    CanCancel = appointment.Status == 0
                };

                _logger.LogInformation($"Customer {customerId} created appointment {appointment.AppointmentId} for pet {appointment.PetId}");
                return CreatedAtAction(nameof(GetAppointment), new { id = appointment.AppointmentId }, response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating appointment");
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi đặt lịch hẹn" });
            }
        }

        /// <summary>
        /// Hủy lịch hẹn (chỉ được phép khi status = 0 - chờ xác nhận)
        /// </summary>
        [HttpDelete("{id}")]
        [AuthorizeRole(0)] // Chỉ khách hàng
        public async Task<IActionResult> CancelAppointment(int id)
        {
            try
            {
                var customerId = await GetCurrentCustomerIdAsync();
                if (customerId == null)
                {
                    return BadRequest(new { message = "Không tìm thấy thông tin khách hàng" });
                }

                var appointment = await _context.Appointments
                    .Include(a => a.Pet)
                    .Where(a => a.AppointmentId == id && a.Pet.CustomerId == customerId.Value)
                    .FirstOrDefaultAsync();

                if (appointment == null)
                {
                    return NotFound(new { message = "Không tìm thấy lịch hẹn hoặc bạn không có quyền truy cập" });
                }

                // Kiểm tra trạng thái có thể hủy không
                if (appointment.Status != 0)
                {
                    var statusText = GetStatusText(appointment.Status);
                    return BadRequest(new { message = $"Không thể hủy lịch hẹn có trạng thái '{statusText}'. Chỉ có thể hủy lịch hẹn đang chờ xác nhận." });
                }

                // Thay đổi status thành 3 (đã hủy)
                appointment.Status = 3;
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Customer {customerId} cancelled appointment {id}");
                return Ok(new { message = "Hủy lịch hẹn thành công" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling appointment {AppointmentId}", id);
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi hủy lịch hẹn" });
            }
        }

        /// <summary>
        /// Lấy Customer ID từ JWT token của user hiện tại
        /// </summary>
        private async Task<int?> GetCurrentCustomerIdAsync()
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!int.TryParse(userIdClaim, out int userId))
                {
                    return null;
                }

                return await _jwtService.GetCustomerIdFromUserIdAsync(userId);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Chuyển đổi status number thành text tiếng Việt
        /// </summary>
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

        #region Admin Methods

        /// <summary>
        /// Lấy tất cả lịch hẹn (dành cho admin)
        /// </summary>
        [HttpGet("admin")]
        [AuthorizeRole(1)] // Chỉ admin
        public async Task<ActionResult<IEnumerable<AppointmentResponseDto>>> GetAllAppointments([FromQuery] int page = 1, [FromQuery] int limit = 10, [FromQuery] int? status = null, [FromQuery] string? date = null)
        {
            try
            {
                var skip = (page - 1) * limit;
                var query = _context.Appointments
                    .Include(a => a.Pet)
                        .ThenInclude(p => p.Customer)
                    .Include(a => a.Doctor)
                    .Include(a => a.Service)
                    .AsQueryable();

                // Filter by status if specified
                if (status.HasValue)
                {
                    query = query.Where(a => a.Status == status.Value);
                }

                // Filter by date if specified
                if (!string.IsNullOrWhiteSpace(date) && DateOnly.TryParse(date, out var filterDate))
                {
                    query = query.Where(a => a.AppointmentDate == filterDate);
                }

                var appointments = await query
                    .Select(a => new AppointmentResponseDto
                    {
                        AppointmentId = a.AppointmentId,
                        PetId = a.PetId,
                        DoctorId = a.DoctorId,
                        ServiceId = a.ServiceId,
                        AppointmentDate = a.AppointmentDate,
                        AppointmentTime = a.AppointmentTime,
                        Weight = a.Weight,
                        Age = a.Age,
                        IsNewPet = a.IsNewPet,
                        Status = a.Status,
                        Notes = a.Notes,
                        CreatedAt = a.CreatedAt,
                        PetName = a.Pet.Name,
                        CustomerName = a.Pet.Customer.CustomerName,
                        DoctorName = a.Doctor != null ? a.Doctor.FullName : null,
                        ServiceName = a.Service.Name,
                        ServiceDescription = a.Service.Description,
                        StatusText = GetStatusText(a.Status),
                        CanCancel = a.Status == 0 || a.Status == 1 // Admin có thể hủy nhiều trạng thái hơn
                    })
                    .OrderByDescending(a => a.CreatedAt)
                    .Skip(skip)
                    .Take(limit)
                    .ToListAsync();

                var totalAppointments = await query.CountAsync();

                _logger.LogInformation($"Admin retrieved {appointments.Count} appointments (page {page})");
                return Ok(new
                {
                    appointments = appointments,
                    pagination = new
                    {
                        page = page,
                        limit = limit,
                        total = totalAppointments,
                        totalPages = (int)Math.Ceiling((double)totalAppointments / limit)
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all appointments for admin");
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi lấy danh sách lịch hẹn" });
            }
        }

        /// <summary>
        /// Tìm kiếm lịch hẹn (dành cho admin)
        /// </summary>
        [HttpGet("admin/search")]
        [AuthorizeRole(1)] // Chỉ admin
        public async Task<ActionResult<IEnumerable<AppointmentResponseDto>>> SearchAppointments([FromQuery] string query, [FromQuery] int page = 1, [FromQuery] int limit = 10)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query))
                {
                    return BadRequest(new { message = "Từ khóa tìm kiếm không được để trống" });
                }

                var skip = (page - 1) * limit;
                var searchQuery = query.ToLower().Trim();

                var appointments = await _context.Appointments
                    .Include(a => a.Pet)
                        .ThenInclude(p => p.Customer)
                    .Include(a => a.Doctor)
                    .Include(a => a.Service)
                    .Where(a => a.Pet.Name.ToLower().Contains(searchQuery) ||
                               a.Pet.Customer.CustomerName.ToLower().Contains(searchQuery) ||
                               a.Service.Name.ToLower().Contains(searchQuery) ||
                               (a.Doctor != null && a.Doctor.FullName.ToLower().Contains(searchQuery)) ||
                               (a.Notes != null && a.Notes.ToLower().Contains(searchQuery)))
                    .Select(a => new AppointmentResponseDto
                    {
                        AppointmentId = a.AppointmentId,
                        PetId = a.PetId,
                        DoctorId = a.DoctorId,
                        ServiceId = a.ServiceId,
                        AppointmentDate = a.AppointmentDate,
                        AppointmentTime = a.AppointmentTime,
                        Weight = a.Weight,
                        Age = a.Age,
                        IsNewPet = a.IsNewPet,
                        Status = a.Status,
                        Notes = a.Notes,
                        CreatedAt = a.CreatedAt,
                        PetName = a.Pet.Name,
                        CustomerName = a.Pet.Customer.CustomerName,
                        DoctorName = a.Doctor != null ? a.Doctor.FullName : null,
                        ServiceName = a.Service.Name,
                        ServiceDescription = a.Service.Description,
                        StatusText = GetStatusText(a.Status),
                        CanCancel = a.Status == 0 || a.Status == 1
                    })
                    .OrderByDescending(a => a.CreatedAt)
                    .Skip(skip)
                    .Take(limit)
                    .ToListAsync();

                var totalResults = await _context.Appointments
                    .Include(a => a.Pet)
                        .ThenInclude(p => p.Customer)
                    .Include(a => a.Doctor)
                    .Include(a => a.Service)
                    .Where(a => a.Pet.Name.ToLower().Contains(searchQuery) ||
                               a.Pet.Customer.CustomerName.ToLower().Contains(searchQuery) ||
                               a.Service.Name.ToLower().Contains(searchQuery) ||
                               (a.Doctor != null && a.Doctor.FullName.ToLower().Contains(searchQuery)) ||
                               (a.Notes != null && a.Notes.ToLower().Contains(searchQuery)))
                    .CountAsync();

                _logger.LogInformation($"Admin searched appointments with query '{query}', found {totalResults} results");
                return Ok(new
                {
                    appointments = appointments,
                    searchQuery = query,
                    pagination = new
                    {
                        page = page,
                        limit = limit,
                        total = totalResults,
                        totalPages = (int)Math.Ceiling((double)totalResults / limit)
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching appointments for admin");
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi tìm kiếm lịch hẹn" });
            }
        }

        /// <summary>
        /// Lấy chi tiết lịch hẹn (dành cho admin)
        /// </summary>
        [HttpGet("admin/{id}")]
        [AuthorizeRole(1)] // Chỉ admin
        public async Task<ActionResult<AppointmentResponseDto>> GetAppointmentAdmin(int id)
        {
            try
            {
                var appointment = await _context.Appointments
                    .Include(a => a.Pet)
                        .ThenInclude(p => p.Customer)
                    .Include(a => a.Doctor)
                    .Include(a => a.Service)
                    .Where(a => a.AppointmentId == id)
                    .Select(a => new AppointmentResponseDto
                    {
                        AppointmentId = a.AppointmentId,
                        PetId = a.PetId,
                        DoctorId = a.DoctorId,
                        ServiceId = a.ServiceId,
                        AppointmentDate = a.AppointmentDate,
                        AppointmentTime = a.AppointmentTime,
                        Weight = a.Weight,
                        Age = a.Age,
                        IsNewPet = a.IsNewPet,
                        Status = a.Status,
                        Notes = a.Notes,
                        CreatedAt = a.CreatedAt,
                        PetName = a.Pet.Name,
                        CustomerName = a.Pet.Customer.CustomerName,
                        DoctorName = a.Doctor != null ? a.Doctor.FullName : null,
                        ServiceName = a.Service.Name,
                        ServiceDescription = a.Service.Description,
                        StatusText = GetStatusText(a.Status),
                        CanCancel = a.Status == 0 || a.Status == 1
                    })
                    .FirstOrDefaultAsync();

                if (appointment == null)
                {
                    return NotFound(new { message = "Không tìm thấy lịch hẹn" });
                }

                _logger.LogInformation($"Admin retrieved appointment {id}");
                return Ok(appointment);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving appointment {AppointmentId} for admin", id);
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi lấy thông tin lịch hẹn" });
            }
        }

        /// <summary>
        /// Tạo lịch hẹn cho khách hàng (dành cho admin)
        /// </summary>
        [HttpPost("admin")]
        [AuthorizeRole(1)] // Chỉ admin
        public async Task<ActionResult<AppointmentResponseDto>> CreateAppointmentAdmin([FromBody] CreateAppointmentDto createDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Kiểm tra thú cưng có tồn tại không
                var pet = await _context.Pets
                    .Include(p => p.Customer)
                    .FirstOrDefaultAsync(p => p.PetId == createDto.PetId);

                if (pet == null)
                {
                    return BadRequest(new { message = "Thú cưng không tồn tại" });
                }

                // Kiểm tra dịch vụ có tồn tại không
                var service = await _context.Services.FindAsync(createDto.ServiceId);
                if (service == null)
                {
                    return BadRequest(new { message = "Dịch vụ không tồn tại" });
                }

                // Kiểm tra bác sĩ (nếu có chỉ định)
                if (createDto.DoctorId.HasValue)
                {
                    var doctor = await _context.Doctors.FindAsync(createDto.DoctorId.Value);
                    if (doctor == null)
                    {
                        return BadRequest(new { message = "Bác sĩ không tồn tại" });
                    }
                }

                // Kiểm tra ngày hẹn không được trong quá khứ
                if (createDto.AppointmentDate < DateOnly.FromDateTime(DateTime.Today))
                {
                    return BadRequest(new { message = "Ngày hẹn không thể trong quá khứ" });
                }

                // Kiểm tra trùng lịch cho cùng thú cưng trong cùng ngày và giờ
                var existingAppointment = await _context.Appointments
                    .AnyAsync(a => a.PetId == createDto.PetId && 
                                  a.AppointmentDate == createDto.AppointmentDate && 
                                  a.AppointmentTime == createDto.AppointmentTime &&
                                  (a.Status == 0 || a.Status == 1));

                if (existingAppointment)
                {
                    return BadRequest(new { message = "Thú cưng đã có lịch hẹn vào thời gian này" });
                }

                var appointment = new Appointment
                {
                    PetId = createDto.PetId,
                    ServiceId = createDto.ServiceId,
                    DoctorId = createDto.DoctorId,
                    AppointmentDate = createDto.AppointmentDate,
                    AppointmentTime = createDto.AppointmentTime,
                    Weight = createDto.Weight,
                    Age = createDto.Age,
                    IsNewPet = createDto.IsNewPet,
                    Status = 0, // Admin tạo lịch cũng để trạng thái "Chờ xác nhận"
                    Notes = createDto.Notes,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Appointments.Add(appointment);
                await _context.SaveChangesAsync();

                // Load thông tin liên quan để trả về
                await _context.Entry(appointment)
                    .Reference(a => a.Pet)
                    .LoadAsync();
                await _context.Entry(appointment.Pet)
                    .Reference(p => p.Customer)
                    .LoadAsync();
                await _context.Entry(appointment)
                    .Reference(a => a.Service)
                    .LoadAsync();
                if (appointment.DoctorId.HasValue)
                {
                    await _context.Entry(appointment)
                        .Reference(a => a.Doctor)
                        .LoadAsync();
                }

                var response = new AppointmentResponseDto
                {
                    AppointmentId = appointment.AppointmentId,
                    PetId = appointment.PetId,
                    DoctorId = appointment.DoctorId,
                    ServiceId = appointment.ServiceId,
                    AppointmentDate = appointment.AppointmentDate,
                    AppointmentTime = appointment.AppointmentTime,
                    Weight = appointment.Weight,
                    Age = appointment.Age,
                    IsNewPet = appointment.IsNewPet,
                    Status = appointment.Status,
                    Notes = appointment.Notes,
                    CreatedAt = appointment.CreatedAt,
                    PetName = appointment.Pet.Name,
                    CustomerName = appointment.Pet.Customer.CustomerName,
                    DoctorName = appointment.Doctor?.FullName,
                    ServiceName = appointment.Service.Name,
                    ServiceDescription = appointment.Service.Description,
                    StatusText = GetStatusText(appointment.Status),
                    CanCancel = appointment.Status == 0 // Chỉ có thể hủy khi đang chờ xác nhận
                };

                _logger.LogInformation($"Admin created appointment {appointment.AppointmentId} for pet {appointment.PetId}");
                return CreatedAtAction(nameof(GetAppointmentAdmin), new { id = appointment.AppointmentId }, response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating appointment for admin");
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi tạo lịch hẹn" });
            }
        }

        /// <summary>
        /// Cập nhật lịch hẹn (dành cho admin)
        /// </summary>
        [HttpPut("admin/{id}")]
        [AuthorizeRole(1)] // Chỉ admin
        public async Task<IActionResult> UpdateAppointmentAdmin(int id, [FromBody] CreateAppointmentDto updateDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var appointment = await _context.Appointments.FindAsync(id);
                if (appointment == null)
                {
                    return NotFound(new { message = "Không tìm thấy lịch hẹn" });
                }

                // Kiểm tra thú cưng có tồn tại không
                var pet = await _context.Pets.FindAsync(updateDto.PetId);
                if (pet == null)
                {
                    return BadRequest(new { message = "Thú cưng không tồn tại" });
                }

                // Kiểm tra dịch vụ có tồn tại không
                var service = await _context.Services.FindAsync(updateDto.ServiceId);
                if (service == null)
                {
                    return BadRequest(new { message = "Dịch vụ không tồn tại" });
                }

                // Kiểm tra bác sĩ (nếu có chỉ định)
                if (updateDto.DoctorId.HasValue)
                {
                    var doctor = await _context.Doctors.FindAsync(updateDto.DoctorId.Value);
                    if (doctor == null)
                    {
                        return BadRequest(new { message = "Bác sĩ không tồn tại" });
                    }
                }

                // Kiểm tra trùng lịch cho cùng thú cưng trong cùng ngày và giờ (trừ lịch hiện tại)
                var existingAppointment = await _context.Appointments
                    .AnyAsync(a => a.PetId == updateDto.PetId && 
                                  a.AppointmentDate == updateDto.AppointmentDate && 
                                  a.AppointmentTime == updateDto.AppointmentTime &&
                                  a.AppointmentId != id &&
                                  (a.Status == 0 || a.Status == 1));

                if (existingAppointment)
                {
                    return BadRequest(new { message = "Thú cưng đã có lịch hẹn khác vào thời gian này" });
                }

                // Cập nhật thông tin
                appointment.PetId = updateDto.PetId;
                appointment.ServiceId = updateDto.ServiceId;
                appointment.DoctorId = updateDto.DoctorId;
                appointment.AppointmentDate = updateDto.AppointmentDate;
                appointment.AppointmentTime = updateDto.AppointmentTime;
                appointment.Weight = updateDto.Weight;
                appointment.Age = updateDto.Age;
                appointment.IsNewPet = updateDto.IsNewPet;
                appointment.Notes = updateDto.Notes;

                await _context.SaveChangesAsync();

                _logger.LogInformation($"Admin updated appointment {id}");
                return Ok(new { message = "Cập nhật lịch hẹn thành công" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating appointment {AppointmentId} for admin", id);
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi cập nhật lịch hẹn" });
            }
        }

        /// <summary>
        /// Cập nhật trạng thái lịch hẹn (dành cho admin)
        /// </summary>
        [HttpPatch("admin/{id}/status")]
        [AuthorizeRole(1)] // Chỉ admin
        public async Task<IActionResult> UpdateAppointmentStatus(int id, [FromBody] int newStatus)
        {
            try
            {
                if (newStatus < 0 || newStatus > 3)
                {
                    return BadRequest(new { message = "Trạng thái phải từ 0 (Chờ xác nhận) đến 3 (Đã hủy)" });
                }

                var appointment = await _context.Appointments
                    .Include(a => a.Pet)
                        .ThenInclude(p => p.Customer)
                            .ThenInclude(c => c.User)
                    .Include(a => a.Service)
                    .Include(a => a.Doctor)
                    .FirstOrDefaultAsync(a => a.AppointmentId == id);

                if (appointment == null)
                {
                    return NotFound(new { message = "Không tìm thấy lịch hẹn" });
                }

                var oldStatus = appointment.Status;
                appointment.Status = newStatus;
                await _context.SaveChangesAsync();

                // Gửi email khi xác nhận lịch hẹn (status thay đổi từ 0 sang 1)
                if (oldStatus == 0 && newStatus == 1)
                {
                    try
                    {
                        var customerEmail = appointment.Pet.Customer.User.Email;
                        if (!string.IsNullOrEmpty(customerEmail))
                        {
                            await _emailService.SendAppointmentConfirmationEmailAsync(
                                customerEmail,
                                appointment.Pet.Customer.CustomerName,
                                appointment.Pet.Name,
                                appointment.Service.Name,
                                appointment.Doctor?.FullName ?? "Chưa chỉ định",
                                appointment.AppointmentDate.ToString("dd/MM/yyyy"),
                                appointment.AppointmentTime
                            );
                        }
                    }
                    catch (Exception emailEx)
                    {
                        _logger.LogError(emailEx, "Failed to send confirmation email for appointment {AppointmentId}", id);
                        // Không throw exception vì việc gửi email không ảnh hưởng đến logic chính
                    }
                }
                // Gửi email thông báo thay đổi trạng thái cho các trường hợp khác
                else if (oldStatus != newStatus && oldStatus.HasValue)
                {
                    try
                    {
                        var customerEmail = appointment.Pet.Customer.User.Email;
                        if (!string.IsNullOrEmpty(customerEmail))
                        {
                            await _emailService.SendAppointmentStatusChangeEmailAsync(
                                customerEmail,
                                appointment.Pet.Customer.CustomerName,
                                appointment.Pet.Name,
                                appointment.Service.Name,
                                appointment.AppointmentDate.ToString("dd/MM/yyyy"),
                                appointment.AppointmentTime,
                                GetStatusText(oldStatus),
                                GetStatusText(newStatus)
                            );
                        }
                    }
                    catch (Exception emailEx)
                    {
                        _logger.LogError(emailEx, "Failed to send status change email for appointment {AppointmentId}", id);
                        // Không throw exception vì việc gửi email không ảnh hưởng đến logic chính
                    }
                }

                _logger.LogInformation($"Admin updated appointment {id} status from {GetStatusText(oldStatus)} to {GetStatusText(newStatus)}");
                return Ok(new { 
                    message = "Cập nhật trạng thái lịch hẹn thành công",
                    oldStatus = GetStatusText(oldStatus),
                    newStatus = GetStatusText(newStatus)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating appointment {AppointmentId} status for admin", id);
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi cập nhật trạng thái lịch hẹn" });
            }
        }

        /// <summary>
        /// Xóa lịch hẹn (dành cho admin)
        /// </summary>
        [HttpDelete("admin/{id}")]
        [AuthorizeRole(1)] // Chỉ admin
        public async Task<IActionResult> DeleteAppointmentAdmin(int id)
        {
            try
            {
                var appointment = await _context.Appointments
                    .Include(a => a.Pet)
                    .ThenInclude(p => p.Customer)
                    .FirstOrDefaultAsync(a => a.AppointmentId == id);

                if (appointment == null)
                {
                    return NotFound(new { message = "Không tìm thấy lịch hẹn" });
                }

                _context.Appointments.Remove(appointment);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Admin deleted appointment {id} for pet {appointment.Pet.Name} (customer: {appointment.Pet.Customer.CustomerName})");
                return Ok(new { message = "Xóa lịch hẹn thành công" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting appointment {AppointmentId} for admin", id);
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi xóa lịch hẹn" });
            }
        }

        #endregion
    }
} 