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
    public class FeedbackController : ControllerBase
    {
        private readonly ThuybinhduongContext _context;
        private readonly IJwtService _jwtService;
        private readonly ILogger<FeedbackController> _logger;

        public FeedbackController(ThuybinhduongContext context, IJwtService jwtService, ILogger<FeedbackController> logger)
        {
            _context = context;
            _jwtService = jwtService;
            _logger = logger;
        }

        #region Customer Endpoints

        /// <summary>
        /// Lấy danh sách đánh giá của khách hàng hiện tại
        /// </summary>
        [HttpGet]
        [AuthorizeRole(0)] // Chỉ khách hàng
        public async Task<ActionResult> GetMyFeedbacks([FromQuery] int page = 1, [FromQuery] int limit = 10)
        {
            try
            {
                var customerId = await GetCurrentCustomerIdAsync();
                if (customerId == null)
                {
                    return BadRequest(new { message = "Không tìm thấy thông tin khách hàng" });
                }

                var skip = (page - 1) * limit;
                var query = _context.Feedbacks
                    .Include(f => f.Appointment)
                        .ThenInclude(a => a.Pet)
                            .ThenInclude(p => p.Customer)
                    .Include(f => f.Appointment)
                        .ThenInclude(a => a.Service)
                    .Include(f => f.Appointment)
                        .ThenInclude(a => a.Doctor)
                    .Where(f => f.Appointment.Pet.CustomerId == customerId.Value);

                var total = await query.CountAsync();
                var feedbacks = await query
                    .OrderByDescending(f => f.CreatedAt)
                    .Skip(skip)
                    .Take(limit)
                    .Select(f => new FeedbackResponseDto
                    {
                        FeedbackId = f.FeedbackId,
                        AppointmentId = f.AppointmentId,
                        Rating = f.Rating ?? 0,
                        Comment = f.Comment,
                        CreatedAt = f.CreatedAt,
                        CustomerName = f.Appointment.Pet.Customer.CustomerName,
                        PetName = f.Appointment.Pet.Name,
                        ServiceName = f.Appointment.Service.Name,
                        AppointmentDate = f.Appointment.AppointmentDate,
                        AppointmentTime = f.Appointment.AppointmentTime,
                        DoctorName = f.Appointment.Doctor != null ? f.Appointment.Doctor.FullName : null
                    })
                    .ToListAsync();

                _logger.LogInformation($"Customer {customerId} retrieved {feedbacks.Count} feedbacks");
                return Ok(new
                {
                    feedbacks = feedbacks,
                    pagination = new
                    {
                        page = page,
                        limit = limit,
                        total = total,
                        totalPages = (int)Math.Ceiling((double)total / limit)
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving customer feedbacks");
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi lấy danh sách đánh giá" });
            }
        }

        /// <summary>
        /// Lấy chi tiết một đánh giá
        /// </summary>
        [HttpGet("{id}")]
        [AuthorizeRole(0)] // Chỉ khách hàng
        public async Task<ActionResult<FeedbackResponseDto>> GetFeedback(int id)
        {
            try
            {
                var customerId = await GetCurrentCustomerIdAsync();
                if (customerId == null)
                {
                    return BadRequest(new { message = "Không tìm thấy thông tin khách hàng" });
                }

                var feedback = await _context.Feedbacks
                    .Include(f => f.Appointment)
                        .ThenInclude(a => a.Pet)
                            .ThenInclude(p => p.Customer)
                    .Include(f => f.Appointment)
                        .ThenInclude(a => a.Service)
                    .Include(f => f.Appointment)
                        .ThenInclude(a => a.Doctor)
                    .Where(f => f.FeedbackId == id && f.Appointment.Pet.CustomerId == customerId.Value)
                    .FirstOrDefaultAsync();

                if (feedback == null)
                {
                    return NotFound(new { message = "Không tìm thấy đánh giá hoặc bạn không có quyền truy cập" });
                }

                var response = new FeedbackResponseDto
                {
                    FeedbackId = feedback.FeedbackId,
                    AppointmentId = feedback.AppointmentId,
                    Rating = feedback.Rating ?? 0,
                    Comment = feedback.Comment,
                    CreatedAt = feedback.CreatedAt,
                    CustomerName = feedback.Appointment.Pet.Customer.CustomerName,
                    PetName = feedback.Appointment.Pet.Name,
                    ServiceName = feedback.Appointment.Service.Name,
                    AppointmentDate = feedback.Appointment.AppointmentDate,
                    AppointmentTime = feedback.Appointment.AppointmentTime,
                    DoctorName = feedback.Appointment.Doctor?.FullName
                };

                _logger.LogInformation($"Customer {customerId} retrieved feedback {id}");
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving feedback {FeedbackId}", id);
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi lấy thông tin đánh giá" });
            }
        }

        /// <summary>
        /// Tạo đánh giá mới cho lịch hẹn đã hoàn thành
        /// </summary>
        [HttpPost]
        [AuthorizeRole(0)] // Chỉ khách hàng
        public async Task<ActionResult<FeedbackResponseDto>> CreateFeedback([FromBody] CreateFeedbackDto createDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();
                    return BadRequest(new { message = "Dữ liệu không hợp lệ", errors = errors });
                }

                var customerId = await GetCurrentCustomerIdAsync();
                if (customerId == null)
                {
                    return BadRequest(new { message = "Không tìm thấy thông tin khách hàng" });
                }

                // Kiểm tra lịch hẹn có tồn tại và thuộc về khách hàng không
                var appointment = await _context.Appointments
                    .Include(a => a.Pet)
                        .ThenInclude(p => p.Customer)
                    .Include(a => a.Service)
                    .Include(a => a.Doctor)
                    .FirstOrDefaultAsync(a => a.AppointmentId == createDto.AppointmentId && 
                                           a.Pet.CustomerId == customerId.Value);

                if (appointment == null)
                {
                    return BadRequest(new { message = "Không tìm thấy lịch hẹn hoặc bạn không có quyền đánh giá" });
                }

                // Kiểm tra lịch hẹn đã hoàn thành chưa
                if (appointment.Status != 2) // Status 2 = Hoàn thành
                {
                    return BadRequest(new { message = "Chỉ có thể đánh giá lịch hẹn đã hoàn thành" });
                }

                // Kiểm tra đã có đánh giá cho lịch hẹn này chưa
                var existingFeedback = await _context.Feedbacks
                    .AnyAsync(f => f.AppointmentId == createDto.AppointmentId);

                if (existingFeedback)
                {
                    return BadRequest(new { message = "Lịch hẹn này đã được đánh giá rồi" });
                }

                var feedback = new Feedback
                {
                    AppointmentId = createDto.AppointmentId,
                    Rating = createDto.Rating,
                    Comment = createDto.Comment,
                    CreatedAt = GetVietnamTime() // Giờ Việt Nam (UTC+7)
                };

                _context.Feedbacks.Add(feedback);
                await _context.SaveChangesAsync();

                // Load lại thông tin để trả về
                await _context.Entry(feedback)
                    .Reference(f => f.Appointment)
                    .LoadAsync();

                var response = new FeedbackResponseDto
                {
                    FeedbackId = feedback.FeedbackId,
                    AppointmentId = feedback.AppointmentId,
                    Rating = feedback.Rating ?? 0,
                    Comment = feedback.Comment,
                    CreatedAt = feedback.CreatedAt,
                    CustomerName = appointment.Pet.Customer.CustomerName,
                    PetName = appointment.Pet.Name,
                    ServiceName = appointment.Service.Name,
                    AppointmentDate = appointment.AppointmentDate,
                    AppointmentTime = appointment.AppointmentTime,
                    DoctorName = appointment.Doctor?.FullName
                };

                _logger.LogInformation($"Customer {customerId} created feedback {feedback.FeedbackId} for appointment {createDto.AppointmentId}");
                return CreatedAtAction(nameof(GetFeedback), new { id = feedback.FeedbackId }, response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating feedback");
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi tạo đánh giá" });
            }
        }

        /// <summary>
        /// Cập nhật đánh giá
        /// </summary>
        [HttpPut("{id}")]
        [AuthorizeRole(0)] // Chỉ khách hàng
        public async Task<IActionResult> UpdateFeedback(int id, [FromBody] UpdateFeedbackDto updateDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();
                    return BadRequest(new { message = "Dữ liệu không hợp lệ", errors = errors });
                }

                var customerId = await GetCurrentCustomerIdAsync();
                if (customerId == null)
                {
                    return BadRequest(new { message = "Không tìm thấy thông tin khách hàng" });
                }

                var feedback = await _context.Feedbacks
                    .Include(f => f.Appointment)
                        .ThenInclude(a => a.Pet)
                    .FirstOrDefaultAsync(f => f.FeedbackId == id && f.Appointment.Pet.CustomerId == customerId.Value);

                if (feedback == null)
                {
                    return NotFound(new { message = "Không tìm thấy đánh giá hoặc bạn không có quyền chỉnh sửa" });
                }

                // Cập nhật thông tin
                feedback.Rating = updateDto.Rating;
                feedback.Comment = updateDto.Comment;

                await _context.SaveChangesAsync();

                _logger.LogInformation($"Customer {customerId} updated feedback {id}");
                return Ok(new { message = "Cập nhật đánh giá thành công" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating feedback {FeedbackId}", id);
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi cập nhật đánh giá" });
            }
        }

        /// <summary>
        /// Xóa đánh giá
        /// </summary>
        [HttpDelete("{id}")]
        [AuthorizeRole(0)] // Chỉ khách hàng
        public async Task<IActionResult> DeleteFeedback(int id)
        {
            try
            {
                var customerId = await GetCurrentCustomerIdAsync();
                if (customerId == null)
                {
                    return BadRequest(new { message = "Không tìm thấy thông tin khách hàng" });
                }

                var feedback = await _context.Feedbacks
                    .Include(f => f.Appointment)
                        .ThenInclude(a => a.Pet)
                    .FirstOrDefaultAsync(f => f.FeedbackId == id && f.Appointment.Pet.CustomerId == customerId.Value);

                if (feedback == null)
                {
                    return NotFound(new { message = "Không tìm thấy đánh giá hoặc bạn không có quyền xóa" });
                }

                _context.Feedbacks.Remove(feedback);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Customer {customerId} deleted feedback {id}");
                return Ok(new { message = "Xóa đánh giá thành công" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting feedback {FeedbackId}", id);
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi xóa đánh giá" });
            }
        }

        #endregion

        #region Admin Endpoints

        /// <summary>
        /// Lấy tất cả đánh giá (dành cho admin)
        /// </summary>
        [HttpGet("admin")]
        [AuthorizeRole(1)] // Chỉ admin
        public async Task<ActionResult> GetAllFeedbacks(
            [FromQuery] int page = 1, 
            [FromQuery] int limit = 10,
            [FromQuery] int? rating = null,
            [FromQuery] string? searchTerm = null)
        {
            try
            {
                var skip = (page - 1) * limit;
                var query = _context.Feedbacks
                    .Include(f => f.Appointment)
                        .ThenInclude(a => a.Pet)
                            .ThenInclude(p => p.Customer)
                    .Include(f => f.Appointment)
                        .ThenInclude(a => a.Service)
                    .Include(f => f.Appointment)
                        .ThenInclude(a => a.Doctor)
                    .AsQueryable();

                // Filter by rating if specified
                if (rating.HasValue && rating.Value >= 1 && rating.Value <= 5)
                {
                    query = query.Where(f => f.Rating == rating.Value);
                }

                // Search filter
                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    var search = searchTerm.ToLower();
                    query = query.Where(f =>
                        f.Appointment.Pet.Customer.CustomerName.ToLower().Contains(search) ||
                        f.Appointment.Pet.Name.ToLower().Contains(search) ||
                        f.Appointment.Service.Name.ToLower().Contains(search) ||
                        (f.Comment != null && f.Comment.ToLower().Contains(search)) ||
                        (f.Appointment.Doctor != null && f.Appointment.Doctor.FullName.ToLower().Contains(search)));
                }

                var total = await query.CountAsync();
                var feedbacks = await query
                    .OrderByDescending(f => f.CreatedAt)
                    .Skip(skip)
                    .Take(limit)
                    .Select(f => new FeedbackResponseDto
                    {
                        FeedbackId = f.FeedbackId,
                        AppointmentId = f.AppointmentId,
                        Rating = f.Rating ?? 0,
                        Comment = f.Comment,
                        CreatedAt = f.CreatedAt,
                        CustomerName = f.Appointment.Pet.Customer.CustomerName,
                        PetName = f.Appointment.Pet.Name,
                        ServiceName = f.Appointment.Service.Name,
                        AppointmentDate = f.Appointment.AppointmentDate,
                        AppointmentTime = f.Appointment.AppointmentTime,
                        DoctorName = f.Appointment.Doctor != null ? f.Appointment.Doctor.FullName : null
                    })
                    .ToListAsync();

                _logger.LogInformation($"Admin retrieved {feedbacks.Count} feedbacks (page {page})");
                return Ok(new
                {
                    feedbacks = feedbacks,
                    pagination = new
                    {
                        page = page,
                        limit = limit,
                        total = total,
                        totalPages = (int)Math.Ceiling((double)total / limit)
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all feedbacks for admin");
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi lấy danh sách đánh giá" });
            }
        }

        /// <summary>
        /// Lấy thống kê đánh giá (dành cho admin)
        /// </summary>
        [HttpGet("admin/statistics")]
        [AuthorizeRole(1)] // Chỉ admin
        public async Task<ActionResult> GetFeedbackStatistics()
        {
            try
            {
                var total = await _context.Feedbacks.CountAsync();
                
                // Calculate average rating safely
                double avgRating = 0;
                if (total > 0)
                {
                    avgRating = await _context.Feedbacks
                        .Where(f => f.Rating.HasValue)
                        .AverageAsync(f => (double)f.Rating!.Value);
                }
                
                var ratingCounts = await _context.Feedbacks
                    .GroupBy(f => f.Rating)
                    .Select(g => new { Rating = g.Key, Count = g.Count() })
                    .ToListAsync();

                var recentFeedbacks = await _context.Feedbacks
                    .Include(f => f.Appointment)
                        .ThenInclude(a => a.Pet)
                            .ThenInclude(p => p.Customer)
                    .Include(f => f.Appointment)
                        .ThenInclude(a => a.Service)
                    .OrderByDescending(f => f.CreatedAt)
                    .Take(5)
                    .Select(f => new FeedbackResponseDto
                    {
                        FeedbackId = f.FeedbackId,
                        AppointmentId = f.AppointmentId,
                        Rating = f.Rating ?? 0,
                        Comment = f.Comment,
                        CreatedAt = f.CreatedAt,
                        CustomerName = f.Appointment.Pet.Customer.CustomerName,
                        PetName = f.Appointment.Pet.Name,
                        ServiceName = f.Appointment.Service.Name
                    })
                    .ToListAsync();

                _logger.LogInformation("Admin retrieved feedback statistics");
                return Ok(new
                {
                    totalFeedbacks = total,
                    averageRating = Math.Round(avgRating, 2),
                    ratingDistribution = ratingCounts,
                    recentFeedbacks = recentFeedbacks
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving feedback statistics");
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi lấy thống kê đánh giá" });
            }
        }

        /// <summary>
        /// Xóa đánh giá (dành cho admin)
        /// </summary>
        [HttpDelete("admin/{id}")]
        [AuthorizeRole(1)] // Chỉ admin
        public async Task<IActionResult> DeleteFeedbackAdmin(int id)
        {
            try
            {
                var feedback = await _context.Feedbacks
                    .Include(f => f.Appointment)
                        .ThenInclude(a => a.Pet)
                            .ThenInclude(p => p.Customer)
                    .FirstOrDefaultAsync(f => f.FeedbackId == id);

                if (feedback == null)
                {
                    return NotFound(new { message = "Không tìm thấy đánh giá" });
                }

                var customerName = feedback.Appointment.Pet.Customer.CustomerName;
                _context.Feedbacks.Remove(feedback);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Admin deleted feedback {id} from customer {customerName}");
                return Ok(new { message = "Xóa đánh giá thành công" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting feedback {FeedbackId} for admin", id);
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi xóa đánh giá" });
            }
        }

        #endregion

        #region Helper Methods

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
        /// Lấy thời gian hiện tại theo giờ Việt Nam (UTC+7)
        /// </summary>
        private static DateTime GetVietnamTime()
        {
            var utcNow = DateTime.UtcNow;
            var vietnamTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            return TimeZoneInfo.ConvertTimeFromUtc(utcNow, vietnamTimeZone);
        }

        #endregion
    }
} 