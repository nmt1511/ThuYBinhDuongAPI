using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThuYBinhDuongAPI.Data.Dtos;
using ThuYBinhDuongAPI.Models;
using ThuYBinhDuongAPI.Services;

namespace ThuYBinhDuongAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ServiceController : ControllerBase
    {
        private readonly ThuybinhduongContext _context;
        private readonly ILogger<ServiceController> _logger;
        private readonly ServiceRecommendationService _recommendationService;
        private readonly WeatherService _weatherService;

        public ServiceController(
            ThuybinhduongContext context, 
            ILogger<ServiceController> logger,
            ServiceRecommendationService recommendationService,
            WeatherService weatherService)
        {
            _context = context;
            _logger = logger;
            _recommendationService = recommendationService;
            _weatherService = weatherService;
        }

        /// <summary>
        /// Lấy danh sách dịch vụ với tìm kiếm (cho dropdown và danh sách)
        /// </summary>
        [HttpGet]
        [AllowAnonymous] // Ai cũng xem được dịch vụ
        public async Task<ActionResult<IEnumerable<ServiceResponseDto>>> GetServices(
            [FromQuery] string? search = null,
            [FromQuery] string? category = null,
            [FromQuery] int page = 1,
            [FromQuery] int limit = 50)
        {
            try
            {
                var query = _context.Services.AsQueryable();

                // Chỉ lấy dịch vụ đang hoạt động
                query = query.Where(s => s.IsActive == true || s.IsActive == null);

                // Tìm kiếm theo tên hoặc mô tả
                if (!string.IsNullOrEmpty(search))
                {
                    var searchLower = search.ToLower();
                    query = query.Where(s => 
                        s.Name.ToLower().Contains(searchLower) ||
                        (s.Description != null && s.Description.ToLower().Contains(searchLower)) ||
                        (s.Category != null && s.Category.ToLower().Contains(searchLower))
                    );
                }

                // Lọc theo danh mục
                if (!string.IsNullOrEmpty(category))
                {
                    query = query.Where(s => s.Category == category);
                }

                // Pagination
                var totalCount = await query.CountAsync();
                var services = await query
                    .Skip((page - 1) * limit)
                    .Take(limit)
                    .Select(s => new ServiceResponseDto
                    {
                        ServiceId = s.ServiceId,
                        Name = s.Name,
                        Description = s.Description,
                        Price = s.Price,
                        Duration = s.Duration,
                        Category = s.Category,
                        IsActive = s.IsActive,
                        DisplayText = CreateDisplayText(s.Name, s.Price, s.Duration),
                        PriceText = CreatePriceText(s.Price),
                        DurationText = CreateDurationText(s.Duration)
                    })
                    .OrderBy(s => s.Category)
                    .ThenBy(s => s.Name)
                    .ToListAsync();

                _logger.LogInformation($"Retrieved {services.Count} services (search: '{search}', category: '{category}')");
                
                return Ok(new 
                {
                    data = services,
                    pagination = new 
                    {
                        page,
                        limit,
                        total = totalCount,
                        totalPages = (int)Math.Ceiling((double)totalCount / limit)
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving services");
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi lấy danh sách dịch vụ" });
            }
        }

        /// <summary>
        /// Lấy danh sách dịch vụ đơn giản cho dropdown (không phân trang)
        /// </summary>
        [HttpGet("dropdown")]
        [AllowAnonymous]
        public async Task<ActionResult<IEnumerable<ServiceResponseDto>>> GetServicesForDropdown(
            [FromQuery] string? search = null)
        {
            try
            {
                var query = _context.Services.AsQueryable();

                // Chỉ lấy dịch vụ đang hoạt động
                query = query.Where(s => s.IsActive == true || s.IsActive == null);

                // Tìm kiếm
                if (!string.IsNullOrEmpty(search))
                {
                    var searchLower = search.ToLower();
                    query = query.Where(s => 
                        s.Name.ToLower().Contains(searchLower) ||
                        (s.Description != null && s.Description.ToLower().Contains(searchLower))
                    );
                }

                var services = await query
                    .Select(s => new ServiceResponseDto
                    {
                        ServiceId = s.ServiceId,
                        Name = s.Name,
                        Description = s.Description,
                        Price = s.Price,
                        Duration = s.Duration,
                        Category = s.Category,
                        IsActive = s.IsActive,
                        DisplayText = CreateDisplayText(s.Name, s.Price, s.Duration),
                        PriceText = CreatePriceText(s.Price),
                        DurationText = CreateDurationText(s.Duration)
                    })
                    .OrderBy(s => s.Name)
                    .Take(20) // Giới hạn 20 cho dropdown
                    .ToListAsync();

                _logger.LogInformation($"Retrieved {services.Count} services for dropdown");
                return Ok(services);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving services for dropdown");
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi lấy danh sách dịch vụ" });
            }
        }

        /// <summary>
        /// Lấy thông tin chi tiết một dịch vụ
        /// </summary>
        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<ActionResult<ServiceResponseDto>> GetService(int id)
        {
            try
            {
                var service = await _context.Services
                    .Where(s => s.ServiceId == id)
                    .Select(s => new ServiceResponseDto
                    {
                        ServiceId = s.ServiceId,
                        Name = s.Name,
                        Description = s.Description,
                        Price = s.Price,
                        Duration = s.Duration,
                        Category = s.Category,
                        IsActive = s.IsActive,
                        DisplayText = CreateDisplayText(s.Name, s.Price, s.Duration),
                        PriceText = CreatePriceText(s.Price),
                        DurationText = CreateDurationText(s.Duration)
                    })
                    .FirstOrDefaultAsync();

                if (service == null)
                {
                    return NotFound(new { message = "Không tìm thấy dịch vụ" });
                }

                _logger.LogInformation($"Retrieved service {id}");
                return Ok(service);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving service {ServiceId}", id);
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi lấy thông tin dịch vụ" });
            }
        }

        /// <summary>
        /// Lấy danh sách các danh mục dịch vụ
        /// </summary>
        [HttpGet("categories")]
        [Authorize]
        public async Task<ActionResult<IEnumerable<string>>> GetServiceCategories()
        {
            try
            {
                var categories = await _context.Services
                    .Where(s => !string.IsNullOrEmpty(s.Category) && (s.IsActive == true || s.IsActive == null))
                    .Select(s => s.Category!)
                    .Distinct()
                    .OrderBy(c => c)
                    .ToListAsync();

                _logger.LogInformation($"Retrieved {categories.Count} service categories");
                return Ok(categories);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving service categories");
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi lấy danh sách danh mục" });
            }
        }

        /// <summary>
        /// Tạo text hiển thị cho dropdown
        /// </summary>
        private static string CreateDisplayText(string name, decimal? price, int? duration)
        {
            var displayText = name;
            
            if (price.HasValue)
            {
                displayText += $" - {price.Value:N0} VNĐ";
            }
            else
            {
                displayText += " - Liên hệ";
            }
            
            return displayText;
        }

        /// <summary>
        /// Tạo text hiển thị giá
        /// </summary>
        private static string CreatePriceText(decimal? price)
        {
            if (price.HasValue)
            {
                return $"{price.Value:N0} VNĐ";
            }
            return "Liên hệ";
        }

        /// <summary>
        /// Tạo text hiển thị thời lượng
        /// </summary>
        private static string CreateDurationText(int? duration)
        {
            if (duration.HasValue)
            {
                return $"{duration.Value} phút";
            }
            return "Liên hệ";
        }

        #region Admin Methods

        /// <summary>
        /// Lấy tất cả dịch vụ (dành cho admin)
        /// </summary>
        [HttpGet("admin")]
        [AuthorizeRole(1)] // Chỉ admin
        public async Task<ActionResult<IEnumerable<ServiceResponseDto>>> GetAllServicesAdmin([FromQuery] int page = 1, [FromQuery] int limit = 15, [FromQuery] bool? isActive = null)
        {
            try
            {
                var skip = (page - 1) * limit;
                var query = _context.Services.AsQueryable();

                // Filter by active status if specified
                if (isActive.HasValue)
                {
                    query = query.Where(s => s.IsActive == isActive.Value);
                }

                var services = await query
                    .Select(s => new ServiceResponseDto
                    {
                        ServiceId = s.ServiceId,
                        Name = s.Name,
                        Description = s.Description,
                        Price = s.Price,
                        Duration = s.Duration,
                        Category = s.Category,
                        IsActive = s.IsActive,
                        DisplayText = CreateDisplayText(s.Name, s.Price, s.Duration),
                        PriceText = CreatePriceText(s.Price),
                        DurationText = CreateDurationText(s.Duration)
                    })
                    .OrderBy(s => s.Category)
                    .ThenBy(s => s.Name)
                    .Skip(skip)
                    .Take(limit)
                    .ToListAsync();

                var totalServices = await query.CountAsync();

                _logger.LogInformation($"Admin retrieved {services.Count} services (page {page})");
                return Ok(new
                {
                    services = services,
                    pagination = new
                    {
                        page = page,
                        limit = limit,
                        total = totalServices,
                        totalPages = (int)Math.Ceiling((double)totalServices / limit)
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all services for admin");
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi lấy danh sách dịch vụ" });
            }
        }

        /// <summary>
        /// Tìm kiếm dịch vụ (dành cho admin)
        /// </summary>
        [HttpGet("admin/search")]
        [AuthorizeRole(1)] // Chỉ admin
        public async Task<ActionResult<IEnumerable<ServiceResponseDto>>> SearchServicesAdmin([FromQuery] string query, [FromQuery] int page = 1, [FromQuery] int limit = 15)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query))
                {
                    return BadRequest(new { message = "Từ khóa tìm kiếm không được để trống" });
                }

                var skip = (page - 1) * limit;
                var searchQuery = query.ToLower().Trim();

                var services = await _context.Services
                    .Where(s => s.Name.ToLower().Contains(searchQuery) ||
                               (s.Description != null && s.Description.ToLower().Contains(searchQuery)) ||
                               (s.Category != null && s.Category.ToLower().Contains(searchQuery)))
                    .Select(s => new ServiceResponseDto
                    {
                        ServiceId = s.ServiceId,
                        Name = s.Name,
                        Description = s.Description,
                        Price = s.Price,
                        Duration = s.Duration,
                        Category = s.Category,
                        IsActive = s.IsActive,
                        DisplayText = CreateDisplayText(s.Name, s.Price, s.Duration),
                        PriceText = CreatePriceText(s.Price),
                        DurationText = CreateDurationText(s.Duration)
                    })
                    .OrderBy(s => s.Category)
                    .ThenBy(s => s.Name)
                    .Skip(skip)
                    .Take(limit)
                    .ToListAsync();

                var totalResults = await _context.Services
                    .Where(s => s.Name.ToLower().Contains(searchQuery) ||
                               (s.Description != null && s.Description.ToLower().Contains(searchQuery)) ||
                               (s.Category != null && s.Category.ToLower().Contains(searchQuery)))
                    .CountAsync();

                _logger.LogInformation($"Admin searched services with query '{query}', found {totalResults} results");
                return Ok(new
                {
                    services = services,
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
                _logger.LogError(ex, "Error searching services for admin");
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi tìm kiếm dịch vụ" });
            }
        }

        /// <summary>
        /// Lấy chi tiết dịch vụ (dành cho admin)
        /// </summary>
        [HttpGet("admin/{id}")]
        [AuthorizeRole(1)] // Chỉ admin
        public async Task<ActionResult<ServiceResponseDto>> GetServiceAdmin(int id)
        {
            try
            {
                var service = await _context.Services
                    .Where(s => s.ServiceId == id)
                    .Select(s => new ServiceResponseDto
                    {
                        ServiceId = s.ServiceId,
                        Name = s.Name,
                        Description = s.Description,
                        Price = s.Price,
                        Duration = s.Duration,
                        Category = s.Category,
                        IsActive = s.IsActive,
                        DisplayText = CreateDisplayText(s.Name, s.Price, s.Duration),
                        PriceText = CreatePriceText(s.Price),
                        DurationText = CreateDurationText(s.Duration)
                    })
                    .FirstOrDefaultAsync();

                if (service == null)
                {
                    return NotFound(new { message = "Không tìm thấy dịch vụ" });
                }

                _logger.LogInformation($"Admin retrieved service {id}");
                return Ok(service);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving service {ServiceId} for admin", id);
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi lấy thông tin dịch vụ" });
            }
        }

        /// <summary>
        /// Tạo dịch vụ mới (dành cho admin)
        /// </summary>
        [HttpPost("admin")]
        [AuthorizeRole(1)] // Chỉ admin
        public async Task<ActionResult<ServiceResponseDto>> CreateServiceAdmin([FromBody] CreateServiceDto createDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Kiểm tra tên dịch vụ đã tồn tại chưa
                var existingService = await _context.Services
                    .AnyAsync(s => s.Name.ToLower() == createDto.Name.ToLower());

                if (existingService)
                {
                    return BadRequest(new { message = "Tên dịch vụ đã tồn tại" });
                }

                var service = new Service
                {
                    Name = createDto.Name,
                    Description = createDto.Description,
                    Price = createDto.Price,
                    Duration = createDto.Duration,
                    Category = createDto.Category,
                    IsActive = createDto.IsActive ?? true
                };

                _context.Services.Add(service);
                await _context.SaveChangesAsync();

                var response = new ServiceResponseDto
                {
                    ServiceId = service.ServiceId,
                    Name = service.Name,
                    Description = service.Description,
                    Price = service.Price,
                    Duration = service.Duration,
                    Category = service.Category,
                    IsActive = service.IsActive,
                    DisplayText = CreateDisplayText(service.Name, service.Price, service.Duration),
                    PriceText = CreatePriceText(service.Price),
                    DurationText = CreateDurationText(service.Duration)
                };

                _logger.LogInformation($"Admin created service {service.ServiceId} - {service.Name}");
                return CreatedAtAction(nameof(GetServiceAdmin), new { id = service.ServiceId }, response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating service for admin");
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi tạo dịch vụ" });
            }
        }

        /// <summary>
        /// Cập nhật dịch vụ (dành cho admin)
        /// </summary>
        [HttpPut("admin/{id}")]
        [AuthorizeRole(1)] // Chỉ admin
        public async Task<IActionResult> UpdateServiceAdmin(int id, [FromBody] CreateServiceDto updateDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var service = await _context.Services.FindAsync(id);
                if (service == null)
                {
                    return NotFound(new { message = "Không tìm thấy dịch vụ" });
                }

                // Kiểm tra tên dịch vụ đã tồn tại chưa (trừ dịch vụ hiện tại)
                var existingService = await _context.Services
                    .AnyAsync(s => s.Name.ToLower() == updateDto.Name.ToLower() && s.ServiceId != id);

                if (existingService)
                {
                    return BadRequest(new { message = "Tên dịch vụ đã tồn tại" });
                }

                // Cập nhật thông tin
                service.Name = updateDto.Name;
                service.Description = updateDto.Description;
                service.Price = updateDto.Price;
                service.Duration = updateDto.Duration;
                service.Category = updateDto.Category;
                service.IsActive = updateDto.IsActive ?? service.IsActive;

                await _context.SaveChangesAsync();

                _logger.LogInformation($"Admin updated service {id}");
                return Ok(new { message = "Cập nhật dịch vụ thành công" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating service {ServiceId} for admin", id);
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi cập nhật dịch vụ" });
            }
        }

        /// <summary>
        /// Cập nhật trạng thái dịch vụ (dành cho admin)
        /// </summary>
        [HttpPatch("admin/{id}/status")]
        [AuthorizeRole(1)] // Chỉ admin
        public async Task<IActionResult> UpdateServiceStatus(int id, [FromBody] bool isActive)
        {
            try
            {
                var service = await _context.Services.FindAsync(id);
                if (service == null)
                {
                    return NotFound(new { message = "Không tìm thấy dịch vụ" });
                }

                var oldStatus = service.IsActive;
                service.IsActive = isActive;
                await _context.SaveChangesAsync();

                var statusText = isActive ? "kích hoạt" : "vô hiệu hóa";
                _logger.LogInformation($"Admin {statusText} service {id}");
                return Ok(new 
                { 
                    message = $"Đã {statusText} dịch vụ thành công",
                    oldStatus = oldStatus == true ? "Đang hoạt động" : "Không hoạt động",
                    newStatus = isActive ? "Đang hoạt động" : "Không hoạt động"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating service {ServiceId} status for admin", id);
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi cập nhật trạng thái dịch vụ" });
            }
        }

        /// <summary>
        /// Xóa dịch vụ (dành cho admin)
        /// </summary>
        [HttpDelete("admin/{id}")]
        [AuthorizeRole(1)] // Chỉ admin
        public async Task<IActionResult> DeleteServiceAdmin(int id)
        {
            try
            {
                var service = await _context.Services.FindAsync(id);
                if (service == null)
                {
                    return NotFound(new { message = "Không tìm thấy dịch vụ" });
                }

                // Kiểm tra xem dịch vụ có đang được sử dụng trong appointments không
                var hasActiveAppointments = await _context.Appointments
                    .AnyAsync(a => a.ServiceId == id && (a.Status == 0 || a.Status == 1)); // Chờ xác nhận hoặc đã xác nhận

                if (hasActiveAppointments)
                {
                    return BadRequest(new { message = "Không thể xóa dịch vụ đang có lịch hẹn hoạt động" });
                }

                _context.Services.Remove(service);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Admin deleted service {id} - {service.Name}");
                return Ok(new { message = "Xóa dịch vụ thành công" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting service {ServiceId} for admin", id);
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi xóa dịch vụ" });
            }
        }

        #endregion

        #region Service Recommendations & Weather

        /// <summary>
        /// Lấy gợi ý dịch vụ dựa trên KNN algorithm cho khách hàng
        /// </summary>
        [HttpGet("recommendations/{customerId}")]
        [Authorize]
        public async Task<ActionResult<List<ServiceRecommendationDto>>> GetServiceRecommendations(int customerId)
        {
            try
            {
                // Kiểm tra customer có tồn tại không
                var customer = await _context.Customers.FindAsync(customerId);
                if (customer == null)
                {
                    return NotFound(new { message = "Không tìm thấy khách hàng" });
                }

                var recommendations = await _recommendationService.GetKNNRecommendationsForCustomer(customerId, k: 5);
                
                return Ok(recommendations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting service recommendations for customer {CustomerId}", customerId);
                return StatusCode(500, new { message = "Lỗi server khi lấy gợi ý dịch vụ" });
            }
        }

        /// <summary>
        /// Lấy thông tin thời tiết hiện tại (cho Bình Dương)
        /// </summary>
        [HttpGet("weather")]
        [AllowAnonymous]
        public async Task<ActionResult<WeatherInfo>> GetWeather()
        {
            try
            {
                var weather = await _weatherService.GetCurrentWeatherAsync();
                if (weather == null)
                {
                    return StatusCode(500, new { message = "Không thể lấy thông tin thời tiết" });
                }
                return Ok(weather);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting weather");
                return StatusCode(500, new { message = "Lỗi server khi lấy thông tin thời tiết" });
            }
        }

        #endregion
    }
} 