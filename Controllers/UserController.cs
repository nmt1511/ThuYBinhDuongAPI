using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using ThuYBinhDuongAPI.Data.Dtos;
using ThuYBinhDuongAPI.Models;
using ThuYBinhDuongAPI.Services;

namespace ThuYBinhDuongAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : ControllerBase
    {
        private readonly ThuybinhduongContext _context;
        private readonly IJwtService _jwtService;
        private readonly ILogger<UserController> _logger;

        public UserController(ThuybinhduongContext context, IJwtService jwtService, ILogger<UserController> logger)
        {
            _context = context;
            _jwtService = jwtService;
            _logger = logger;
        }

        /// <summary>
        /// Đăng ký tài khoản mới
        /// </summary>
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto registerDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Kiểm tra username đã tồn tại
                if (await _context.Users.AnyAsync(u => u.Username == registerDto.Username))
                {
                    return BadRequest(new { message = "Tên đăng nhập đã tồn tại" });
                }

                // Kiểm tra email đã tồn tại
                if (await _context.Users.AnyAsync(u => u.Email == registerDto.Email))
                {
                    return BadRequest(new { message = "Email đã được sử dụng" });
                }

                // Mã hóa mật khẩu
                var passwordHash = HashPassword(registerDto.Password);

                // Tạo user mới
                var user = new User
                {
                    Username = registerDto.Username,
                    Password = passwordHash,
                    Email = registerDto.Email,
                    PhoneNumber = registerDto.PhoneNumber,
                    Role = registerDto.Role,
                    CreatedAt = DateTime.UtcNow
                };

                // Bắt đầu transaction để tạo cả User và Customer
                using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    _context.Users.Add(user);
                    await _context.SaveChangesAsync(); // Lưu để có UserId

                    // Nếu role = 0 (khách hàng), tạo thêm record Customer
                    if (registerDto.Role == 0)
                    {
                        var customer = new Customer
                        {
                            UserId = user.UserId,
                            CustomerName = registerDto.CustomerName,
                            Address = registerDto.Address,
                            Gender = registerDto.Gender
                        };

                        _context.Customers.Add(customer);
                        await _context.SaveChangesAsync();
                    }

                    await transaction.CommitAsync();
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }

                // Tạo JWT token
                var token = _jwtService.GenerateToken(user);

                var response = new UserResponseDto
                {
                    UserId = user.UserId,
                    Username = user.Username,
                    Email = user.Email,
                    PhoneNumber = user.PhoneNumber,
                    Role = user.Role,
                    CreatedAt = user.CreatedAt,
                    Token = token
                };

                _logger.LogInformation($"User {user.Username} registered successfully");
                return CreatedAtAction(nameof(GetProfile), new { id = user.UserId }, response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during user registration");
                return StatusCode(500, new { message = "Đã xảy ra lỗi trong quá trình đăng ký" });
            }
        }

        /// <summary>
        /// Đăng nhập
        /// </summary>
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto loginDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Tìm user theo username
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Username == loginDto.Username);

                if (user == null || !VerifyPassword(loginDto.Password, user.Password))
                {
                    return Unauthorized(new { message = "Tên đăng nhập hoặc mật khẩu không đúng" });
                }

                // Tạo JWT token
                var token = _jwtService.GenerateToken(user);

                var response = new UserResponseDto
                {
                    UserId = user.UserId,
                    Username = user.Username,
                    Email = user.Email,
                    PhoneNumber = user.PhoneNumber,
                    Role = user.Role,
                    CreatedAt = user.CreatedAt,
                    Token = token
                };

                _logger.LogInformation($"User {user.Username} logged in successfully");
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during user login");
                return StatusCode(500, new { message = "Đã xảy ra lỗi trong quá trình đăng nhập" });
            }
        }

                 /// <summary>
         /// Đăng xuất (chỉ trả về thông báo vì JWT là stateless)
         /// </summary>
         [HttpPost("logout")]
         [Authorize]
         public IActionResult Logout()
         {
             try
             {
                 var username = User.FindFirst(ClaimTypes.Name)?.Value ?? 
                              User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                 _logger.LogInformation($"User {username} logged out");
                 return Ok(new { message = "Đăng xuất thành công" });
             }
             catch (Exception ex)
             {
                 _logger.LogError(ex, "Error during logout");
                 return StatusCode(500, new { message = "Đã xảy ra lỗi trong quá trình đăng xuất" });
             }
         }

        /// <summary>
        /// Lấy thông tin profile của user hiện tại
        /// </summary>
        [HttpGet("profile")]
        [Authorize]
        public async Task<IActionResult> GetProfile()
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!int.TryParse(userIdClaim, out int userId))
                {
                    return Unauthorized(new { message = "Token không hợp lệ" });
                }

                // Include Customer info
                var user = await _context.Users.Include(u => u.Customers).FirstOrDefaultAsync(u => u.UserId == userId);
                if (user == null)
                {
                    return NotFound(new { message = "Không tìm thấy người dùng" });
                }

                var customer = user.Customers.FirstOrDefault();
                var response = new UserResponseDto
                {
                    UserId = user.UserId,
                    Username = user.Username,
                    Email = user.Email,
                    PhoneNumber = user.PhoneNumber,
                    Role = user.Role,
                    CreatedAt = user.CreatedAt,
                    CustomerId = customer?.CustomerId, // Thêm customer_id
                    CustomerName = customer?.CustomerName,
                    Address = customer?.Address,
                    Gender = customer?.Gender
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user profile");
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi lấy thông tin người dùng" });
            }
        }

        /// <summary>
        /// Lấy thông tin profile theo ID (chỉ dành cho admin)
        /// </summary>
        [HttpGet("profile/{id}")]
        [AuthorizeRole(1)] // Chỉ Administrator (Role = 1)
        public async Task<IActionResult> GetProfile(int id)
        {
            try
            {
                var user = await _context.Users.FindAsync(id);
                if (user == null)
                {
                    return NotFound(new { message = "Không tìm thấy người dùng" });
                }

                var response = new UserResponseDto
                {
                    UserId = user.UserId,
                    Username = user.Username,
                    Email = user.Email,
                    PhoneNumber = user.PhoneNumber,
                    Role = user.Role,
                    CreatedAt = user.CreatedAt,
                    CustomerName = user.Customers.FirstOrDefault()?.CustomerName,
                    Address = user.Customers.FirstOrDefault()?.Address,
                    Gender = user.Customers.FirstOrDefault()?.Gender
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user profile by ID");
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi lấy thông tin người dùng" });
            }
        }

                 /// <summary>
         /// Kiểm tra token có hợp lệ không
         /// </summary>
         [HttpGet("validate-token")]
         [Authorize]
         public IActionResult ValidateToken()
         {
             try
             {
                 var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                 var username = User.FindFirst(ClaimTypes.Name)?.Value;
                 var role = User.FindFirst("Role")?.Value;
                 var roleName = User.FindFirst("RoleName")?.Value;
                 
                 return Ok(new 
                 { 
                     valid = true,
                     userId = userId,
                     username = username,
                     role = role,
                     roleName = roleName,
                     message = "Token hợp lệ" 
                 });
             }
             catch (Exception ex)
             {
                 _logger.LogError(ex, "Error validating token");
                 return StatusCode(500, new { message = "Đã xảy ra lỗi khi kiểm tra token" });
             }
         }

         /// <summary>
         /// Lấy danh sách tất cả users (chỉ dành cho admin)
         /// </summary>
         [HttpGet("list")]
         [AuthorizeRole(1)] // Chỉ Administrator
         public async Task<IActionResult> GetAllUsers([FromQuery] int page = 1, [FromQuery] int limit = 15, [FromQuery] string? search = null)
         {
             try
             {
                 var skip = (page - 1) * limit;
                 var query = _context.Users
                     .Include(u => u.Customers) // Include Customer data
                     .AsQueryable();

                 // Apply search filter if provided
                 if (!string.IsNullOrWhiteSpace(search))
                 {
                     query = query.Where(u => 
                         u.Username.Contains(search) ||
                         u.Email.Contains(search) ||
                         (u.PhoneNumber != null && u.PhoneNumber.Contains(search)) ||
                         u.Customers.Any(c => c.CustomerName.Contains(search))
                     );
                 }

                 var users = await query
                     .OrderByDescending(u => u.CreatedAt)
                     .Skip(skip)
                     .Take(limit)
                     .Select(u => new 
                     {
                         UserId = u.UserId,
                         Username = u.Username,
                         Email = u.Email,
                         PhoneNumber = u.PhoneNumber,
                         Role = u.Role,
                         RoleName = u.Role == 0 ? "Customer" : u.Role == 1 ? "Administrator" : "Doctor",
                         CreatedAt = u.CreatedAt,
                         // Include Customer information if user is a customer
                         
                     })
                     .ToListAsync();

                 var totalUsers = await query.CountAsync();

                 return Ok(new
                 {
                     users = users,
                     pagination = new
                     {
                         page = page,
                         limit = limit,
                         total = totalUsers,
                         totalPages = (int)Math.Ceiling((double)totalUsers / limit)
                     }
                 });
             }
             catch (Exception ex)
             {
                 _logger.LogError(ex, "Error getting users list");
                 return StatusCode(500, new { message = "Đã xảy ra lỗi khi lấy danh sách người dùng" });
             }
         }

         /// <summary>
         /// Cập nhật role của user (chỉ dành cho admin)
         /// </summary>
                   [HttpPut("update-role/{userId}")]
         [AuthorizeRole(1)] // Chỉ Administrator
         public async Task<IActionResult> UpdateUserRole(int userId, [FromBody] int newRole)
         {
             try
             {
                 if (newRole < 0 || newRole > 2)
                 {
                     return BadRequest(new { message = "Role phải là 0 (Customer), 1 (Administrator), hoặc 2 (Doctor/Other)" });
                 }

                 var user = await _context.Users.FindAsync(userId);
                 if (user == null)
                 {
                     return NotFound(new { message = "Không tìm thấy người dùng" });
                 }

                 user.Role = newRole;
                 await _context.SaveChangesAsync();

                 _logger.LogInformation($"Admin updated role of user {user.Username} to {newRole}");
                 return Ok(new { message = "Cập nhật role thành công", newRole = newRole });
             }
             catch (Exception ex)
             {
                 _logger.LogError(ex, "Error updating user role");
                 return StatusCode(500, new { message = "Đã xảy ra lỗi khi cập nhật role" });
             }
         }

                 /// <summary>
         /// Cập nhật thông tin người dùng (Admin only)
         /// </summary>
         [HttpPut("update/{userId}")]
         [AuthorizeRole(1)] // Chỉ Administrator
         public async Task<IActionResult> UpdateUser(int userId, [FromBody] UpdateUserDto updateUserDto)
         {
             try
             {
                 var user = await _context.Users.Include(u => u.Customers).FirstOrDefaultAsync(u => u.UserId == userId);
                 if (user == null)
                 {
                     return NotFound(new { message = "Không tìm thấy người dùng" });
                 }

                 // Update User fields
                 if (!string.IsNullOrWhiteSpace(updateUserDto.Email))
                     user.Email = updateUserDto.Email;
                 
                 if (!string.IsNullOrWhiteSpace(updateUserDto.PhoneNumber))
                     user.PhoneNumber = updateUserDto.PhoneNumber;

                 // Update Customer fields if user is a customer (role = 0)
                 if (user.Role == 0 && user.Customers.Any())
                 {
                     var customer = user.Customers.First();
                     
                     if (!string.IsNullOrWhiteSpace(updateUserDto.CustomerName))
                         customer.CustomerName = updateUserDto.CustomerName;
                     
                     if (!string.IsNullOrWhiteSpace(updateUserDto.Address))
                         customer.Address = updateUserDto.Address;
                     
                     if (updateUserDto.Gender.HasValue)
                         customer.Gender = updateUserDto.Gender.Value;
                 }

                 await _context.SaveChangesAsync();
                 _logger.LogInformation("User {UserId} updated successfully by admin", userId);

                 // Return updated user info
                 var response = new UserResponseDto
                 {
                     UserId = user.UserId,
                     Username = user.Username,
                     Email = user.Email,
                     PhoneNumber = user.PhoneNumber,
                     Role = user.Role,
                     CreatedAt = user.CreatedAt,
                     CustomerName = user.Customers.FirstOrDefault()?.CustomerName,
                     Address = user.Customers.FirstOrDefault()?.Address,
                     Gender = user.Customers.FirstOrDefault()?.Gender
                 };

                 return Ok(response);
             }
             catch (Exception ex)
             {
                 _logger.LogError(ex, "Error updating user {UserId}", userId);
                 return StatusCode(500, new { message = "Đã xảy ra lỗi khi cập nhật người dùng" });
             }
         }

         /// <summary>
         /// Đổi mật khẩu cho user hiện tại
         /// </summary>
         [HttpPost("change-password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
        {
            try
            {
                var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (!int.TryParse(userIdClaim, out int userId))
                {
                    return Unauthorized(new { message = "Token không hợp lệ" });
                }

                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    return NotFound(new { message = "Không tìm thấy người dùng" });
                }

                // Kiểm tra mật khẩu cũ
                if (!VerifyPassword(dto.OldPassword, user.Password))
                {
                    return BadRequest(new { message = "Mật khẩu cũ không đúng" });
                }

                // Kiểm tra mật khẩu mới khác mật khẩu cũ
                if (dto.OldPassword == dto.NewPassword)
                {
                    return BadRequest(new { message = "Mật khẩu mới phải khác mật khẩu cũ" });
                }

                // Cập nhật mật khẩu mới
                user.Password = HashPassword(dto.NewPassword);
                await _context.SaveChangesAsync();
                return Ok(new { message = "Đổi mật khẩu thành công" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing password for user");
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi đổi mật khẩu" });
            }
        }

        /// <summary>
        /// Cập nhật thông tin cá nhân của khách hàng (Customer)
        /// </summary>
        [HttpPut("update-customer")]
        [Authorize(Roles = "0")] // Chỉ cho phép khách hàng
        public async Task<IActionResult> UpdateCustomer([FromBody] UpdateCustomerDto dto)
        {
            try
            {
                var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (!int.TryParse(userIdClaim, out int userId))
                {
                    return Unauthorized(new { message = "Token không hợp lệ" });
                }

                var customer = await _context.Customers.FirstOrDefaultAsync(c => c.UserId == userId);
                if (customer == null)
                {
                    return NotFound(new { message = "Không tìm thấy thông tin khách hàng" });
                }

                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    return NotFound(new { message = "Không tìm thấy thông tin người dùng" });
                }

                // Kiểm tra email đã tồn tại cho user khác chưa
                if (await _context.Users.AnyAsync(u => u.Email == dto.Email && u.UserId != userId))
                {
                    return BadRequest(new { message = "Email đã được sử dụng bởi người dùng khác" });
                }
                // Kiểm tra số điện thoại đã tồn tại cho user khác chưa (nếu có nhập)
                if (!string.IsNullOrEmpty(dto.PhoneNumber) && await _context.Users.AnyAsync(u => u.PhoneNumber == dto.PhoneNumber && u.UserId != userId))
                {
                    return BadRequest(new { message = "Số điện thoại đã được sử dụng bởi người dùng khác" });
                }

                // Cập nhật thông tin Customer
                customer.CustomerName = dto.CustomerName;
                customer.Address = dto.Address;
                customer.Gender = dto.Gender;

                // Cập nhật thông tin User
                user.Email = dto.Email;
                user.PhoneNumber = dto.PhoneNumber;

                await _context.SaveChangesAsync();

                return Ok(new { message = "Cập nhật thông tin cá nhân thành công" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating customer info");
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi cập nhật thông tin cá nhân" });
            }
        }

        /// <summary>
        /// Xóa người dùng (Admin only)
        /// </summary>
        [HttpDelete("delete/{userId}")]
        [AuthorizeRole(1)] // Chỉ Administrator
        public async Task<IActionResult> DeleteUser(int userId)
        {
            _logger.LogInformation($"DeleteUser called for userId: {userId}");
            try
            {
                var user = await _context.Users
                    .Include(u => u.Customers)
                    .ThenInclude(c => c.Pets)
                    .FirstOrDefaultAsync(u => u.UserId == userId);

                if (user == null)
                {
                    return NotFound(new { message = "Không tìm thấy người dùng" });
                }

                // Không cho phép xóa tài khoản admin đang đăng nhập
                var currentUserIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (int.TryParse(currentUserIdClaim, out int currentUserId) && currentUserId == userId)
                {
                    return BadRequest(new { message = "Không thể xóa tài khoản đang đăng nhập" });
                }

                using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    _logger.LogInformation($"Starting deletion transaction for user {userId}");

                    // Xóa tất cả dữ liệu liên quan nếu là customer
                    if (user.Customers.Any())
                    {
                        // Convert to list để tránh lỗi "Collection was modified" khi enumerate
                        var customersList = user.Customers.ToList();
                        _logger.LogInformation($"User has {customersList.Count} customer(s)");
                        
                        foreach (var customer in customersList)
                        {
                            _logger.LogInformation($"Processing customer {customer.CustomerId}");

                            // BƯỚC 1: Xóa ChatRooms trước (vì có foreign key đến Customer)
                            try
                            {
                                var chatRooms = await _context.ChatRooms
                                    .Where(cr => cr.CustomerId == customer.CustomerId)
                                    .ToListAsync();
                                
                                _logger.LogInformation($"Found {chatRooms.Count} chat room(s) for customer {customer.CustomerId}");
                                
                                if (chatRooms.Any())
                                {
                                    _context.ChatRooms.RemoveRange(chatRooms);
                                    await _context.SaveChangesAsync();
                                    _logger.LogInformation($"Deleted {chatRooms.Count} chat room(s)");
                                }
                            }
                            catch (Exception chatRoomEx)
                            {
                                _logger.LogWarning(chatRoomEx, "Error deleting chat rooms for customer {CustomerId}: {Message}", customer.CustomerId, chatRoomEx.Message);
                            }

                            // BƯỚC 2: Xóa pets và dữ liệu liên quan
                            if (customer.Pets.Any())
                            {
                                // Convert to list để tránh lỗi "Collection was modified"
                                var petsList = customer.Pets.ToList();
                                _logger.LogInformation($"Customer {customer.CustomerId} has {petsList.Count} pet(s)");
                                
                                foreach (var pet in petsList)
                                {
                                    _logger.LogInformation($"Processing pet {pet.PetId}");

                                    // Xóa feedbacks của appointments
                                    var petAppointments = await _context.Appointments
                                        .Include(a => a.Feedbacks)
                                        .Where(a => a.PetId == pet.PetId)
                                        .ToListAsync();

                                    _logger.LogInformation($"Pet {pet.PetId} has {petAppointments.Count} appointment(s)");

                                    foreach (var appointment in petAppointments)
                                    {
                                        if (appointment.Feedbacks.Any())
                                        {
                                            _logger.LogInformation($"Deleting {appointment.Feedbacks.Count} feedback(s) for appointment {appointment.AppointmentId}");
                                            _context.Feedbacks.RemoveRange(appointment.Feedbacks);
                                        }
                                    }

                                    // Xóa appointments
                                    if (petAppointments.Any())
                                    {
                                        _context.Appointments.RemoveRange(petAppointments);
                                    }

                                    // Xóa medical histories
                                    var medicalHistories = await _context.MedicalHistories
                                        .Where(m => m.PetId == pet.PetId)
                                        .ToListAsync();
                                    
                                    if (medicalHistories.Any())
                                    {
                                        _logger.LogInformation($"Deleting {medicalHistories.Count} medical history record(s) for pet {pet.PetId}");
                                        _context.MedicalHistories.RemoveRange(medicalHistories);
                                    }
                                }
                                
                                // Xóa tất cả pets (dùng petsList thay vì customer.Pets)
                                if (petsList.Any())
                                {
                                    _context.Pets.RemoveRange(petsList);
                                    await _context.SaveChangesAsync();
                                    _logger.LogInformation($"Deleted all pets for customer {customer.CustomerId}");
                                }
                            }

                            // BƯỚC 3: Xóa customer
                            _context.Customers.Remove(customer);
                            await _context.SaveChangesAsync();
                            _logger.LogInformation($"Deleted customer {customer.CustomerId}");
                        }
                    }

                    // BƯỚC 4: Xóa user
                    _context.Users.Remove(user);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation($"Deleted user {userId}");

                    await transaction.CommitAsync();
                    _logger.LogInformation($"Transaction committed successfully for user {userId}");

                    return Ok(new { message = "Xóa người dùng thành công" });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in transaction while deleting user {UserId}: {Message}. StackTrace: {StackTrace}", 
                        userId, ex.Message, ex.StackTrace);
                    
                    try
                    {
                        await transaction.RollbackAsync();
                        _logger.LogInformation($"Transaction rolled back for user {userId}");
                    }
                    catch (Exception rollbackEx)
                    {
                        _logger.LogError(rollbackEx, "Error rolling back transaction for user {UserId}", userId);
                    }
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user {UserId}", userId);
                return StatusCode(500, new { message = "Không thể xóa người dùng. Vui lòng thử lại." });
            }
        }

        #region Private Methods

        /// <summary>
        /// Mã hóa mật khẩu bằng SHA256
        /// </summary>
        private string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                return Convert.ToBase64String(hashedBytes);
            }
        }

        /// <summary>
        /// Xác minh mật khẩu
        /// </summary>
        private bool VerifyPassword(string password, string hashedPassword)
        {
            var hashOfInput = HashPassword(password);
            return hashOfInput == hashedPassword;
        }

        #endregion
    }
} 