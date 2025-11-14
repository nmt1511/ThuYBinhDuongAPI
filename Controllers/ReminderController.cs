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

        /// <summary>
        /// Test endpoint để kiểm tra reminders sắp tới (Admin only)
        /// </summary>
        [HttpGet("upcoming-reminders")]
        [AuthorizeRole(1)] // Admin only
        public async Task<IActionResult> GetUpcomingReminders()
        {
            try
            {
                var upcomingReminders = await _reminderService.GetUpcomingRemindersAsync();

                return Ok(new
                {
                    success = true,
                    message = $"Tìm thấy {upcomingReminders.Count} lời nhắc hẹn sắp tới",
                    reminders = upcomingReminders
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting upcoming reminders");
                return StatusCode(500, new { message = "Lỗi khi lấy danh sách nhắc hẹn" });
            }
        }

        /// <summary>
        /// Lấy danh sách reminders cho user cụ thể (Admin only)
        /// </summary>
        [HttpGet("user-reminders/{userId}")]
        [AuthorizeRole(1)] // Admin only
        public async Task<IActionResult> GetUserReminders(int userId)
        {
            try
            {
                var userReminders = await _reminderService.GetUserRemindersAsync(userId);

                return Ok(new
                {
                    success = true,
                    message = $"Tìm thấy {userReminders.Count} lời nhắc hẹn cho user {userId}",
                    reminders = userReminders
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user reminders for user {UserId}", userId);
                return StatusCode(500, new { message = "Lỗi khi lấy danh sách nhắc hẹn của user" });
            }
        }

        /// <summary>
        /// Gửi reminders cho user cụ thể (Admin only)
        /// </summary>
        [HttpPost("send-user-reminders/{userId}")]
        [AuthorizeRole(1)] // Admin only
        public async Task<IActionResult> SendUserReminders(int userId)
        {
            try
            {
                var sentCount = await _reminderService.CheckAndSendRemindersForUserAsync(userId);

                return Ok(new
                {
                    success = true,
                    message = $"Đã gửi {sentCount} lời nhắc hẹn cho user {userId}",
                    remindersSent = sentCount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending reminders for user {UserId}", userId);
                return StatusCode(500, new { message = "Lỗi khi gửi nhắc hẹn cho user" });
            }
        }

        /// <summary>
        /// Lấy danh sách tất cả users có reminders sắp tới (Admin only)
        /// </summary>
        [HttpGet("users-with-reminders")]
        [AuthorizeRole(1)] // Admin only
        public async Task<IActionResult> GetUsersWithReminders()
        {
            try
            {
                var usersWithReminders = await _reminderService.GetUsersWithRemindersAsync();

                return Ok(new
                {
                    success = true,
                    message = $"Tìm thấy {usersWithReminders.Count} users có nhắc hẹn sắp tới",
                    users = usersWithReminders
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting users with reminders");
                return StatusCode(500, new { message = "Lỗi khi lấy danh sách users có nhắc hẹn" });
            }
        }

        /// <summary>
        /// Reset trạng thái gửi reminders (Admin only - để test)
        /// </summary>
        [HttpPost("reset-reminder-status")]
        [AuthorizeRole(1)] // Admin only
        public async Task<IActionResult> ResetReminderStatus()
        {
            try
            {
                var resetCount = await _reminderService.ResetReminderStatusAsync();

                return Ok(new
                {
                    success = true,
                    message = $"Đã reset {resetCount} reminders để có thể gửi lại",
                    resetCount = resetCount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting reminder status");
                return StatusCode(500, new { message = "Lỗi khi reset trạng thái reminders" });
            }
        }
    }
}

