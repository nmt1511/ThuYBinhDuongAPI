using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ThuYBinhDuongAPI.Services;

namespace ThuYBinhDuongAPI.Controllers
{
    /// <summary>
    /// Controller quản lý reminders cho next appointments
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class ReminderController : ControllerBase
    {
        private readonly IReminderService _reminderService;
        private readonly ILogger<ReminderController> _logger;

        public ReminderController(IReminderService reminderService, ILogger<ReminderController> logger)
        {
            _reminderService = reminderService;
            _logger = logger;
        }

        /// <summary>
        /// Kiểm tra và gửi reminders cho user hiện tại (gọi khi user mở app)
        /// </summary>
        [HttpPost("check-my-reminders")]
        [Authorize]
        public async Task<IActionResult> CheckMyReminders()
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    return Unauthorized(new { message = "Không thể xác thực người dùng" });
                }

                var sentCount = await _reminderService.CheckAndSendRemindersForUserAsync(userId);

                return Ok(new
                {
                    success = true,
                    message = sentCount > 0 
                        ? $"Đã gửi {sentCount} lời nhắc hẹn" 
                        : "Không có lịch hẹn nào cần nhắc trong 7 ngày tới",
                    remindersSent = sentCount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking reminders");
                return StatusCode(500, new { message = "Lỗi khi kiểm tra lời nhắc hẹn" });
            }
        }

        /// <summary>
        /// Kiểm tra và gửi reminders cho TẤT CẢ users (Admin only - có thể dùng cho scheduled job)
        /// </summary>
        [HttpPost("check-all-reminders")]
        [AuthorizeRole(1)] // Admin only
        public async Task<IActionResult> CheckAllReminders()
        {
            try
            {
                var sentCount = await _reminderService.CheckAndSendRemindersAsync();

                return Ok(new
                {
                    success = true,
                    message = $"Đã gửi {sentCount} lời nhắc hẹn cho tất cả người dùng",
                    remindersSent = sentCount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking all reminders");
                return StatusCode(500, new { message = "Lỗi khi kiểm tra lời nhắc hẹn" });
            }
        }
    }
}

