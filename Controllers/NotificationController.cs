using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using ThuYBinhDuongAPI.Models;

namespace ThuYBinhDuongAPI.Controllers
{
    /// <summary>
    /// Controller quản lý notifications (Local notification approach)
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class NotificationController : ControllerBase
    {
        private readonly ThuybinhduongContext _context;
        private readonly ILogger<NotificationController> _logger;

        public NotificationController(ThuybinhduongContext context, ILogger<NotificationController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Lấy danh sách notifications của user (chưa đọc và đã đọc)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetMyNotifications(
            [FromQuery] bool? isRead = null, 
            [FromQuery] int page = 1, 
            [FromQuery] int limit = 20)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    return Unauthorized(new { message = "Không thể xác thực người dùng" });
                }

                var query = _context.Notifications
                    .Where(n => n.UserId == userId);

                // Filter by read status if specified
                if (isRead.HasValue)
                {
                    query = query.Where(n => n.IsRead == isRead.Value);
                }

                var totalCount = await query.CountAsync();
                
                var notifications = await query
                    .OrderByDescending(n => n.CreatedAt)
                    .Skip((page - 1) * limit)
                    .Take(limit)
                    .Select(n => new
                    {
                        n.NotificationId,
                        n.Title,
                        n.Body,
                        n.Type,
                        n.Data,
                        n.IsRead,
                        n.CreatedAt,
                        n.ReadAt
                    })
                    .ToListAsync();

                return Ok(new
                {
                    notifications,
                    totalCount,
                    page,
                    limit,
                    totalPages = (int)Math.Ceiling(totalCount / (double)limit)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting notifications");
                return StatusCode(500, new { message = "Lỗi khi lấy danh sách thông báo" });
            }
        }

        /// <summary>
        /// Lấy số lượng notifications chưa đọc
        /// </summary>
        [HttpGet("unread-count")]
        public async Task<IActionResult> GetUnreadCount()
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    return Unauthorized(new { message = "Không thể xác thực người dùng" });
                }

                var unreadCount = await _context.Notifications
                    .Where(n => n.UserId == userId && !n.IsRead)
                    .CountAsync();

                return Ok(new { unreadCount });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting unread count");
                return StatusCode(500, new { message = "Lỗi khi lấy số thông báo chưa đọc" });
            }
        }

        /// <summary>
        /// Đánh dấu một notification đã đọc
        /// </summary>
        [HttpPatch("{id}/read")]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    return Unauthorized(new { message = "Không thể xác thực người dùng" });
                }

                var notification = await _context.Notifications
                    .FirstOrDefaultAsync(n => n.NotificationId == id && n.UserId == userId);

                if (notification == null)
                {
                    return NotFound(new { message = "Không tìm thấy thông báo" });
                }

                if (!notification.IsRead)
                {
                    notification.IsRead = true;
                    notification.ReadAt = DateTime.Now;
                    await _context.SaveChangesAsync();
                }

                return Ok(new { message = "Đã đánh dấu đã đọc", success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error marking notification {id} as read");
                return StatusCode(500, new { message = "Lỗi khi đánh dấu đã đọc" });
            }
        }

        /// <summary>
        /// Đánh dấu tất cả notifications đã đọc
        /// </summary>
        [HttpPost("read-all")]
        public async Task<IActionResult> MarkAllAsRead()
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    return Unauthorized(new { message = "Không thể xác thực người dùng" });
                }

                var unreadNotifications = await _context.Notifications
                    .Where(n => n.UserId == userId && !n.IsRead)
                    .ToListAsync();

                foreach (var notification in unreadNotifications)
                {
                    notification.IsRead = true;
                    notification.ReadAt = DateTime.Now;
                }

                await _context.SaveChangesAsync();

                return Ok(new 
                { 
                    message = "Đã đánh dấu tất cả thông báo là đã đọc", 
                    count = unreadNotifications.Count,
                    success = true 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking all notifications as read");
                return StatusCode(500, new { message = "Lỗi khi đánh dấu tất cả đã đọc" });
            }
        }

        /// <summary>
        /// Xóa một notification
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteNotification(int id)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    return Unauthorized(new { message = "Không thể xác thực người dùng" });
                }

                var notification = await _context.Notifications
                    .FirstOrDefaultAsync(n => n.NotificationId == id && n.UserId == userId);

                if (notification == null)
                {
                    return NotFound(new { message = "Không tìm thấy thông báo" });
                }

                _context.Notifications.Remove(notification);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Đã xóa thông báo", success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting notification {id}");
                return StatusCode(500, new { message = "Lỗi khi xóa thông báo" });
            }
        }
    }
}

