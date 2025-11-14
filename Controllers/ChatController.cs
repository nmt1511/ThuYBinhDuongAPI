using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using ThuYBinhDuongAPI.Models;

namespace ThuYBinhDuongAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ChatController : ControllerBase
    {
        private readonly ThuybinhduongContext _context;
        private readonly ILogger<ChatController> _logger;

        public ChatController(ThuybinhduongContext context, ILogger<ChatController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Tạo phòng chat mới hoặc lấy phòng chat hiện có
        /// </summary>
        [HttpPost("room")]
        [AuthorizeRole(0)] // Chỉ khách hàng
        public async Task<ActionResult<object>> CreateOrGetChatRoom()
        {
            try
            {
                var customerId = await GetCurrentCustomerIdAsync();
                if (customerId == null)
                {
                    return BadRequest(new { message = "Không tìm thấy thông tin khách hàng" });
                }

                // Tìm phòng chat hiện có của khách hàng
                var existingRoom = await _context.ChatRooms
                    .FirstOrDefaultAsync(cr => cr.CustomerId == customerId.Value);

                if (existingRoom != null)
                {
                    return Ok(new
                    {
                        roomId = existingRoom.RoomId,
                        roomName = existingRoom.RoomName,
                        status = existingRoom.Status,
                        unreadCount = existingRoom.UnreadCountCustomer,
                        lastMessage = existingRoom.LastMessage,
                        lastMessageAt = existingRoom.LastMessageAt
                    });
                }

                // Tạo phòng chat mới
                var vietnamTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"));
                var newRoom = new ChatRoom
                {
                    CustomerId = customerId.Value,
                    RoomName = $"Tư vấn - {vietnamTime:dd/MM/yyyy HH:mm}",
                    Status = 0, // Chờ admin phản hồi
                    CreatedAt = vietnamTime,
                    UnreadCountCustomer = 0,
                    UnreadCountAdmin = 0
                };

                _context.ChatRooms.Add(newRoom);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Created new chat room {newRoom.RoomId} for customer {customerId}");
                return Ok(new
                {
                    roomId = newRoom.RoomId,
                    roomName = newRoom.RoomName,
                    status = newRoom.Status,
                    unreadCount = newRoom.UnreadCountCustomer,
                    lastMessage = newRoom.LastMessage,
                    lastMessageAt = newRoom.LastMessageAt
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating/getting chat room");
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi tạo phòng chat" });
            }
        }

        /// <summary>
        /// Gửi tin nhắn
        /// </summary>
        [HttpPost("message")]
        [AuthorizeRole(0)] // Chỉ khách hàng
        public async Task<ActionResult<object>> SendMessage([FromBody] SendMessageDto dto)
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

                // Kiểm tra phòng chat có tồn tại và thuộc về khách hàng không
                var room = await _context.ChatRooms
                    .FirstOrDefaultAsync(cr => cr.RoomId == dto.RoomId && cr.CustomerId == customerId.Value);

                if (room == null)
                {
                    return NotFound(new { message = "Không tìm thấy phòng chat" });
                }

                // Tạo tin nhắn mới
                var vietnamTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"));
                var message = new ChatMessage
                {
                    RoomId = dto.RoomId,
                    SenderId = customerId.Value,
                    SenderType = 0, // 0: Customer, 1: Admin
                    MessageContent = dto.MessageContent,
                    MessageType = dto.MessageType ?? 0, // 0: Text, 1: Image, 2: File
                    FileUrl = dto.FileUrl,
                    IsRead = false,
                    CreatedAt = vietnamTime
                };

                _context.ChatMessages.Add(message);

                // Cập nhật thông tin phòng chat
                room.LastMessage = dto.MessageContent.Length > 100 ? 
                    dto.MessageContent.Substring(0, 100) + "..." : dto.MessageContent;
                room.LastMessageAt = vietnamTime;
                // Không tăng UnreadCountAdmin ở đây vì sẽ tăng trong raw SQL

                // Lưu message trước
                await _context.SaveChangesAsync();
                
                // Cập nhật ChatRoom bằng raw SQL để tránh conflict
                await _context.Database.ExecuteSqlRawAsync(
                    "UPDATE ChatRoom SET last_message = {0}, last_message_at = {1}, unread_count_admin = unread_count_admin + 1 WHERE room_id = {2}",
                    room.LastMessage, room.LastMessageAt, room.RoomId);

                _logger.LogInformation($"Customer {customerId} sent message to room {dto.RoomId}");
                return Ok(new
                {
                    messageId = message.MessageId,
                    roomId = message.RoomId,
                    senderId = message.SenderId,
                    senderType = message.SenderType,
                    messageContent = message.MessageContent,
                    messageType = message.MessageType,
                    fileUrl = message.FileUrl,
                    isRead = message.IsRead,
                    createdAt = message.CreatedAt
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message: {Message}", ex.Message);
                _logger.LogError("Stack trace: {StackTrace}", ex.StackTrace);
                
                // Log thêm thông tin về request
                _logger.LogError("Request details - RoomId: {RoomId}, MessageContent: {MessageContent}", 
                    dto.RoomId, dto.MessageContent);
                
                return StatusCode(500, new { 
                    message = "Đã xảy ra lỗi khi gửi tin nhắn",
                    error = ex.Message,
                    details = ex.InnerException?.Message
                });
            }
        }

        /// <summary>
        /// Lấy danh sách tin nhắn trong phòng chat
        /// </summary>
        [HttpGet("room/{roomId}/messages")]
        [AuthorizeRole(0)] // Chỉ khách hàng
        public async Task<ActionResult<object>> GetChatMessages(int roomId, [FromQuery] int limit = 20, [FromQuery] int? beforeMessageId = null)
        {
            try
            {
                var customerId = await GetCurrentCustomerIdAsync();
                if (customerId == null)
                {
                    return BadRequest(new { message = "Không tìm thấy thông tin khách hàng" });
                }

                // Kiểm tra phòng chat có tồn tại và thuộc về khách hàng không
                var room = await _context.ChatRooms
                    .FirstOrDefaultAsync(cr => cr.RoomId == roomId && cr.CustomerId == customerId.Value);

                if (room == null)
                {
                    return NotFound(new { message = "Không tìm thấy phòng chat" });
                }

                var query = _context.ChatMessages
                    .Where(cm => cm.RoomId == roomId);

                // Nếu có beforeMessageId, chỉ lấy tin nhắn cũ hơn tin nhắn đó
                if (beforeMessageId.HasValue)
                {
                    var beforeMessage = await _context.ChatMessages.FindAsync(beforeMessageId.Value);
                    if (beforeMessage != null && beforeMessage.RoomId == roomId)
                    {
                        query = query.Where(cm => cm.CreatedAt < beforeMessage.CreatedAt);
                    }
                }

                // Lấy tin nhắn mới nhất trước, sắp xếp giảm dần theo thời gian
                var messages = await query
                    .OrderByDescending(cm => cm.CreatedAt)
                    .ThenByDescending(cm => cm.MessageId)
                    .Take(limit)
                    .Select(cm => new
                    {
                        messageId = cm.MessageId,
                        roomId = cm.RoomId,
                        senderId = cm.SenderId,
                        senderType = cm.SenderType,
                        messageContent = cm.MessageContent,
                        messageType = cm.MessageType,
                        fileUrl = cm.FileUrl,
                        isRead = cm.IsRead,
                        createdAt = cm.CreatedAt,
                        senderName = cm.SenderType == 0 ? "Bạn" : "Admin"
                    })
                    .ToListAsync();

                // Đảo ngược để sắp xếp theo thứ tự thời gian tăng dần (cũ -> mới) cho frontend
                messages.Reverse();

                // Đánh dấu tin nhắn đã đọc (sử dụng raw SQL)
                await _context.Database.ExecuteSqlRawAsync(
                    "UPDATE ChatMessage SET is_read = 1 WHERE room_id = {0} AND sender_id != {1} AND sender_type != {2}",
                    new object[] { roomId, customerId.Value, 0 });

                // Reset unread count cho customer
                await _context.Database.ExecuteSqlRawAsync(
                    "UPDATE ChatRoom SET unread_count_customer = 0 WHERE room_id = {0}",
                    new object[] { roomId });

                var totalMessages = await _context.ChatMessages.CountAsync(cm => cm.RoomId == roomId);
                var oldestMessageId = messages.Count > 0 ? messages[0].messageId : (int?)null;
                var hasMore = false;
                if (oldestMessageId.HasValue)
                {
                    var oldestMessage = await _context.ChatMessages.FindAsync(oldestMessageId.Value);
                    if (oldestMessage != null)
                    {
                        hasMore = await _context.ChatMessages
                            .AnyAsync(cm => cm.RoomId == roomId && cm.CreatedAt < oldestMessage.CreatedAt);
                    }
                }

                _logger.LogInformation($"Customer {customerId} retrieved {messages.Count} messages from room {roomId}");
                return Ok(new
                {
                    messages = messages,
                    pagination = new
                    {
                        limit = limit,
                        total = totalMessages,
                        hasMore = hasMore,
                        oldestMessageId = oldestMessageId
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving chat messages");
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi lấy tin nhắn" });
            }
        }

        /// <summary>
        /// Lấy danh sách phòng chat (dành cho admin)
        /// </summary>
        [HttpGet("admin/rooms")]
        [AuthorizeRole(1)] // Chỉ admin
        public async Task<ActionResult<object>> GetAdminChatRooms([FromQuery] int page = 1, [FromQuery] int limit = 20)
        {
            try
            {
                var adminId = await GetCurrentUserIdAsync();
                if (adminId == null)
                {
                    _logger.LogWarning("Admin ID not found in token");
                    return BadRequest(new { message = "Không tìm thấy thông tin admin" });
                }

                _logger.LogInformation($"Getting chat rooms for admin {adminId}");

                var skip = (page - 1) * limit;
                
                // Simplified query - get all chat rooms first
                var allRooms = await _context.ChatRooms
                    .Where(cr => cr.AdminUserId == adminId.Value || cr.AdminUserId == null)
                    .OrderByDescending(cr => cr.LastMessageAt ?? cr.CreatedAt)
                    .Skip(skip)
                    .Take(limit)
                    .ToListAsync();

                _logger.LogInformation($"Found {allRooms.Count} chat rooms");

                // Build response with customer info
                var rooms = new List<object>();
                foreach (var room in allRooms)
                {
                    var customer = await _context.Customers
                        .FirstOrDefaultAsync(c => c.CustomerId == room.CustomerId);
                    
                    var user = customer != null ? await _context.Users
                        .FirstOrDefaultAsync(u => u.UserId == customer.UserId) : null;

                    rooms.Add(new
                    {
                        roomId = room.RoomId,
                        customerId = room.CustomerId,
                        adminUserId = room.AdminUserId,
                        roomName = room.RoomName,
                        status = room.Status,
                        createdAt = room.CreatedAt,
                        lastMessageAt = room.LastMessageAt,
                        lastMessage = room.LastMessage,
                        unreadCount = room.UnreadCountAdmin,
                        customerName = customer?.CustomerName ?? "Unknown",
                        customerEmail = user?.Email ?? "",
                        customerPhone = user?.PhoneNumber ?? ""
                    });
                }

                var totalRooms = await _context.ChatRooms
                    .Where(cr => cr.AdminUserId == adminId.Value || cr.AdminUserId == null)
                    .CountAsync();

                _logger.LogInformation($"Admin {adminId} retrieved {rooms.Count} chat rooms");
                return Ok(new
                {
                    rooms = rooms,
                    pagination = new
                    {
                        page = page,
                        limit = limit,
                        total = totalRooms,
                        totalPages = (int)Math.Ceiling((double)totalRooms / limit)
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving admin chat rooms: {Message}", ex.Message);
                _logger.LogError("Stack trace: {StackTrace}", ex.StackTrace);
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi lấy danh sách phòng chat" });
            }
        }

        /// <summary>
        /// Admin nhận phòng chat
        /// </summary>
        [HttpPost("admin/room/{roomId}/assign")]
        [AuthorizeRole(1)] // Chỉ admin
        public async Task<ActionResult> AssignChatRoom(int roomId)
        {
            try
            {
                var adminId = await GetCurrentUserIdAsync();
                if (adminId == null)
                {
                    return BadRequest(new { message = "Không tìm thấy thông tin admin" });
                }

                var room = await _context.ChatRooms.FindAsync(roomId);
                if (room == null)
                {
                    return NotFound(new { message = "Không tìm thấy phòng chat" });
                }

                if (room.AdminUserId != null)
                {
                    return BadRequest(new { message = "Phòng chat đã được admin khác nhận" });
                }

                room.AdminUserId = adminId.Value;
                room.Status = 1; // Đang xử lý
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Admin {adminId} assigned to room {roomId}");
                return Ok(new { message = "Nhận phòng chat thành công" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error assigning chat room");
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi nhận phòng chat" });
            }
        }

        /// <summary>
        /// Admin gửi tin nhắn
        /// </summary>
        [HttpPost("admin/message")]
        [AuthorizeRole(1)] // Chỉ admin
        public async Task<ActionResult<object>> SendAdminMessage([FromBody] SendMessageDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var adminId = await GetCurrentUserIdAsync();
                if (adminId == null)
                {
                    return BadRequest(new { message = "Không tìm thấy thông tin admin" });
                }

                // Kiểm tra phòng chat có tồn tại và admin có quyền truy cập không
                var room = await _context.ChatRooms
                    .FirstOrDefaultAsync(cr => cr.RoomId == dto.RoomId && cr.AdminUserId == adminId.Value);

                if (room == null)
                {
                    return NotFound(new { message = "Không tìm thấy phòng chat hoặc bạn không có quyền truy cập" });
                }

                // Tạo tin nhắn mới
                var vietnamTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"));
                var message = new ChatMessage
                {
                    RoomId = dto.RoomId,
                    SenderId = adminId.Value,
                    SenderType = 1, // 1: Admin
                    MessageContent = dto.MessageContent,
                    MessageType = dto.MessageType ?? 0,
                    FileUrl = dto.FileUrl,
                    IsRead = false,
                    CreatedAt = vietnamTime
                };

                _context.ChatMessages.Add(message);

                // Cập nhật thông tin phòng chat
                room.LastMessage = dto.MessageContent.Length > 100 ? 
                    dto.MessageContent.Substring(0, 100) + "..." : dto.MessageContent;
                room.LastMessageAt = vietnamTime;
                room.UnreadCountCustomer++; // Tăng số tin nhắn chưa đọc của khách hàng

                await _context.SaveChangesAsync();

                _logger.LogInformation($"Admin {adminId} sent message to room {dto.RoomId}");
                return Ok(new
                {
                    messageId = message.MessageId,
                    roomId = message.RoomId,
                    senderId = message.SenderId,
                    senderType = message.SenderType,
                    messageContent = message.MessageContent,
                    messageType = message.MessageType,
                    fileUrl = message.FileUrl,
                    isRead = message.IsRead,
                    createdAt = message.CreatedAt
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending admin message");
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi gửi tin nhắn" });
            }
        }

        /// <summary>
        /// Lấy thông tin chi tiết phòng chat (dành cho admin)
        /// </summary>
        [HttpGet("admin/room/{roomId}")]
        [AuthorizeRole(1)] // Chỉ admin
        public async Task<ActionResult<object>> GetChatRoomDetails(int roomId)
        {
            try
            {
                var adminId = await GetCurrentUserIdAsync();
                if (adminId == null)
                {
                    return BadRequest(new { message = "Không tìm thấy thông tin admin" });
                }

                var room = await _context.ChatRooms
                    .Where(cr => cr.RoomId == roomId && (cr.AdminUserId == adminId.Value || cr.AdminUserId == null))
                    .Select(cr => new
                    {
                        roomId = cr.RoomId,
                        customerId = cr.CustomerId,
                        adminUserId = cr.AdminUserId,
                        roomName = cr.RoomName,
                        status = cr.Status,
                        createdAt = cr.CreatedAt,
                        lastMessageAt = cr.LastMessageAt,
                        lastMessage = cr.LastMessage,
                        unreadCount = cr.UnreadCountAdmin,
                        customerName = _context.Customers
                            .Where(c => c.CustomerId == cr.CustomerId)
                            .Select(c => c.CustomerName)
                            .FirstOrDefault(),
                        customerEmail = _context.Customers
                            .Where(c => c.CustomerId == cr.CustomerId)
                            .Join(_context.Users, c => c.UserId, u => u.UserId, (c, u) => u.Email)
                            .FirstOrDefault(),
                        customerPhone = _context.Customers
                            .Where(c => c.CustomerId == cr.CustomerId)
                            .Join(_context.Users, c => c.UserId, u => u.UserId, (c, u) => u.PhoneNumber)
                            .FirstOrDefault()
                    })
                    .FirstOrDefaultAsync();

                if (room == null)
                {
                    return NotFound(new { message = "Không tìm thấy phòng chat" });
                }

                return Ok(room);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving chat room details");
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi lấy thông tin phòng chat" });
            }
        }

        /// <summary>
        /// Cập nhật trạng thái phòng chat (dành cho admin)
        /// </summary>
        [HttpPatch("admin/room/{roomId}/status")]
        [AuthorizeRole(1)] // Chỉ admin
        public async Task<ActionResult> UpdateChatRoomStatus(int roomId, [FromBody] UpdateChatRoomStatusDto dto)
        {
            try
            {
                var adminId = await GetCurrentUserIdAsync();
                if (adminId == null)
                {
                    return BadRequest(new { message = "Không tìm thấy thông tin admin" });
                }

                var room = await _context.ChatRooms
                    .FirstOrDefaultAsync(cr => cr.RoomId == roomId && cr.AdminUserId == adminId.Value);

                if (room == null)
                {
                    return NotFound(new { message = "Không tìm thấy phòng chat hoặc bạn không có quyền truy cập" });
                }

                room.Status = dto.Status;
                room.UpdatedAt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"));
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Admin {adminId} updated room {roomId} status to {dto.Status}");
                return Ok(new { message = "Cập nhật trạng thái thành công" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating chat room status");
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi cập nhật trạng thái" });
            }
        }

        /// <summary>
        /// Lấy User ID từ JWT token
        /// </summary>
        private Task<int?> GetCurrentUserIdAsync()
        {
            try
            {
                _logger.LogInformation("Getting current user ID from token");
                
                // Try different claim types
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim))
                {
                    userIdClaim = User.FindFirst("UserId")?.Value;
                }
                if (string.IsNullOrEmpty(userIdClaim))
                {
                    userIdClaim = User.FindFirst("user_id")?.Value;
                }
                
                _logger.LogInformation($"User ID claim value: {userIdClaim}");
                
                if (int.TryParse(userIdClaim, out int userId))
                {
                    _logger.LogInformation($"Successfully parsed user ID: {userId}");
                    return Task.FromResult<int?>(userId);
                }
                
                _logger.LogWarning("Could not parse user ID from token");
                return Task.FromResult<int?>(null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current user ID from token");
                return Task.FromResult<int?>(null);
            }
        }

        /// <summary>
        /// Lấy Customer ID từ User ID
        /// </summary>
        private async Task<int?> GetCurrentCustomerIdAsync()
        {
            try
            {
                var userId = await GetCurrentUserIdAsync();
                if (userId == null) return null;

                var customer = await _context.Customers
                    .FirstOrDefaultAsync(c => c.UserId == userId);
                
                // Nếu chưa có Customer record, tự động tạo
                if (customer == null)
                {
                    var user = await _context.Users.FindAsync(userId);
                    if (user == null) return null;

                    customer = new Customer
                    {
                        UserId = userId.Value,
                        CustomerName = user.Username, // Dùng username làm tên khách hàng
                        Gender = 0, // Mặc định
                        Address = "Chưa cập nhật"
                    };

                    _context.Customers.Add(customer);
                    await _context.SaveChangesAsync();
                }
                
                return customer.CustomerId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting customer ID");
                return null;
            }
        }

        /// <summary>
        /// Lấy danh sách tin nhắn trong phòng chat (dành cho admin)
        /// </summary>
        [HttpGet("admin/room/{roomId}/messages")]
        [AuthorizeRole(1)] // Chỉ admin
        public async Task<ActionResult<object>> GetAdminChatMessages(int roomId, [FromQuery] int limit = 20, [FromQuery] int? beforeMessageId = null)
        {
            try
            {
                var adminId = await GetCurrentUserIdAsync();
                if (adminId == null)
                {
                    return BadRequest(new { message = "Không tìm thấy thông tin admin" });
                }

                // Kiểm tra phòng chat có tồn tại và admin có quyền truy cập không
                var room = await _context.ChatRooms
                    .FirstOrDefaultAsync(cr => cr.RoomId == roomId && (cr.AdminUserId == adminId.Value || cr.AdminUserId == null));

                if (room == null)
                {
                    return NotFound(new { message = "Không tìm thấy phòng chat" });
                }

                var query = _context.ChatMessages
                    .Where(cm => cm.RoomId == roomId);

                // Nếu có beforeMessageId, chỉ lấy tin nhắn cũ hơn tin nhắn đó
                if (beforeMessageId.HasValue)
                {
                    var beforeMessage = await _context.ChatMessages.FindAsync(beforeMessageId.Value);
                    if (beforeMessage != null && beforeMessage.RoomId == roomId)
                    {
                        query = query.Where(cm => cm.CreatedAt < beforeMessage.CreatedAt);
                    }
                }

                // Lấy tin nhắn mới nhất trước, sắp xếp giảm dần theo thời gian
                var messages = await query
                    .OrderByDescending(cm => cm.CreatedAt)
                    .ThenByDescending(cm => cm.MessageId)
                    .Take(limit)
                    .Select(cm => new
                    {
                        messageId = cm.MessageId,
                        roomId = cm.RoomId,
                        senderId = cm.SenderId,
                        senderType = cm.SenderType,
                        messageContent = cm.MessageContent,
                        messageType = cm.MessageType,
                        fileUrl = cm.FileUrl,
                        isRead = cm.IsRead,
                        createdAt = cm.CreatedAt,
                        senderName = cm.SenderType == 1 ? "Admin" : "Khách hàng"
                    })
                    .ToListAsync();

                // Đảo ngược để sắp xếp theo thứ tự thời gian tăng dần (cũ -> mới) cho frontend
                messages.Reverse();

                // Đánh dấu tin nhắn đã đọc cho admin
                await _context.Database.ExecuteSqlRawAsync(
                    "UPDATE ChatMessage SET is_read = 1 WHERE room_id = {0} AND sender_id != {1} AND sender_type != {2}",
                    new object[] { roomId, adminId.Value, 1 });

                // Reset unread count cho admin
                await _context.Database.ExecuteSqlRawAsync(
                    "UPDATE ChatRoom SET unread_count_admin = 0 WHERE room_id = {0}",
                    new object[] { roomId });

                var totalMessages = await _context.ChatMessages.CountAsync(cm => cm.RoomId == roomId);
                var oldestMessageId = messages.Count > 0 ? messages[0].messageId : (int?)null;
                var hasMore = false;
                if (oldestMessageId.HasValue)
                {
                    var oldestMessage = await _context.ChatMessages.FindAsync(oldestMessageId.Value);
                    if (oldestMessage != null)
                    {
                        hasMore = await _context.ChatMessages
                            .AnyAsync(cm => cm.RoomId == roomId && cm.CreatedAt < oldestMessage.CreatedAt);
                    }
                }

                _logger.LogInformation($"Admin {adminId} retrieved {messages.Count} messages from room {roomId}");
                return Ok(new
                {
                    messages = messages,
                    pagination = new
                    {
                        limit = limit,
                        total = totalMessages,
                        hasMore = hasMore,
                        oldestMessageId = oldestMessageId
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving admin chat messages");
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi lấy tin nhắn" });
            }
        }

        /// <summary>
        /// Upload ảnh lên Cloudinary (dành cho admin)
        /// </summary>
        [HttpPost("upload/cloudinary")]
        [AuthorizeRole(1)] // Chỉ admin
        public async Task<ActionResult<object>> UploadImage(IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    return BadRequest(new { message = "Không có file được chọn" });
                }

                // Validate file type
                var allowedTypes = new[] { "image/jpeg", "image/png", "image/gif", "image/webp" };
                if (!allowedTypes.Contains(file.ContentType))
                {
                    return BadRequest(new { message = "Chỉ hỗ trợ file ảnh (JPG, PNG, GIF, WEBP)" });
                }

                // Validate file size (5MB max)
                if (file.Length > 5 * 1024 * 1024)
                {
                    return BadRequest(new { message = "Kích thước file không được vượt quá 5MB" });
                }

                // Upload to Cloudinary (implement your Cloudinary upload logic)
                var imageUrl = await UploadToCloudinary(file);
                
                _logger.LogInformation($"Image uploaded successfully: {imageUrl}");
                return Ok(new { url = imageUrl });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading image");
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi tải lên ảnh" });
            }
        }

        /// <summary>
        /// Upload file to Cloudinary
        /// </summary>
        private async Task<string> UploadToCloudinary(IFormFile file)
        {
            try
            {
                // Read file content
                using var memoryStream = new MemoryStream();
                await file.CopyToAsync(memoryStream);
                var fileBytes = memoryStream.ToArray();

                // Convert to base64
                var base64String = Convert.ToBase64String(fileBytes);
                var dataUrl = $"data:{file.ContentType};base64,{base64String}";

                // For now, return a placeholder URL
                // In production, implement actual Cloudinary upload
                var fileName = $"{Guid.NewGuid()}_{file.FileName}";
                var imageUrl = $"https://res.cloudinary.com/your-cloud-name/image/upload/{fileName}";
                
                _logger.LogInformation($"Uploaded image: {fileName}");
                
                // TODO: Implement actual Cloudinary upload
                // You can use CloudinaryDotNet library or direct HTTP calls
                
                return imageUrl;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing file for Cloudinary upload");
                throw;
            }
        }

        /// <summary>
        /// Mark messages as read (Admin)
        /// </summary>
        [HttpPost("admin/room/{roomId}/mark-read")]
        [AuthorizeRole(1)] // Admin only
        public async Task<IActionResult> MarkMessagesAsRead(int roomId)
    {
        try
        {
            var room = await _context.ChatRooms.FindAsync(roomId);
            if (room == null)
            {
                return NotFound(new { message = "Không tìm thấy phòng chat" });
            }

            // Reset unread count for admin
            room.UnreadCountAdmin = 0;
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Admin marked all messages as read in room {roomId}");

            return Ok(new { message = "Đã đánh dấu tất cả tin nhắn là đã đọc" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error marking messages as read in room {roomId}");
            return StatusCode(500, new { message = "Đã xảy ra lỗi" });
        }
    }

    /// <summary>
    /// Mark messages as read (Customer)
    /// </summary>
    [HttpPost("room/{roomId}/mark-read")]
    [AuthorizeRole(0)] // Customer only
    public async Task<IActionResult> MarkMessagesAsReadCustomer(int roomId)
    {
        try
        {
            var customerId = await GetCurrentCustomerIdAsync();
            if (customerId == null)
            {
                return BadRequest(new { message = "Không tìm thấy thông tin khách hàng" });
            }

            var room = await _context.ChatRooms
                .FirstOrDefaultAsync(r => r.RoomId == roomId && r.CustomerId == customerId.Value);

            if (room == null)
            {
                return NotFound(new { message = "Không tìm thấy phòng chat" });
            }

            // Reset unread count for customer
            room.UnreadCountCustomer = 0;
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Customer {customerId} marked all messages as read in room {roomId}");

            return Ok(new { message = "Đã đánh dấu tất cả tin nhắn là đã đọc" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error marking messages as read in room {roomId}");
            return StatusCode(500, new { message = "Đã xảy ra lỗi" });
        }
    }
    }

    // DTOs
    public class SendMessageDto
    {
        public int RoomId { get; set; }
        public string MessageContent { get; set; } = string.Empty;
        public int? MessageType { get; set; }
        public string? FileUrl { get; set; }
    }

    public class UpdateChatRoomStatusDto
    {
        public int Status { get; set; }
    }
}
