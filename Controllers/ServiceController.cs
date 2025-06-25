using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThuYBinhDuongAPI.Data.Dtos;
using ThuYBinhDuongAPI.Models;

namespace ThuYBinhDuongAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ServiceController : ControllerBase
    {
        private readonly ThuybinhduongContext _context;
        private readonly ILogger<ServiceController> _logger;

        public ServiceController(ThuybinhduongContext context, ILogger<ServiceController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Lấy danh sách dịch vụ với tìm kiếm (cho dropdown và danh sách)
        /// </summary>
        [HttpGet]
        [Authorize] // Cần đăng nhập để xem dịch vụ
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
        [Authorize]
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
        [Authorize]
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
    }
} 