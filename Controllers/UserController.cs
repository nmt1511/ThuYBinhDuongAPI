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

                var user = await _context.Users.FindAsync(userId);
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
                    CreatedAt = user.CreatedAt
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
                    CreatedAt = user.CreatedAt
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
         public async Task<IActionResult> GetAllUsers([FromQuery] int page = 1, [FromQuery] int limit = 10, [FromQuery] string? search = null)
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
                         CustomerName = u.Customers.FirstOrDefault() != null ? u.Customers.FirstOrDefault()!.CustomerName : null,
                         Address = u.Customers.FirstOrDefault() != null ? u.Customers.FirstOrDefault()!.Address : null,
                         Gender = u.Customers.FirstOrDefault() != null ? u.Customers.FirstOrDefault()!.Gender : null,
                         CustomerId = u.Customers.FirstOrDefault() != null ? u.Customers.FirstOrDefault()!.CustomerId : (int?)null
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
         /// Tạo dữ liệu mẫu (Development only)
         /// </summary>
         [HttpPost("seed-data")]
         [AuthorizeRole(1)] // Chỉ Administrator
         public async Task<IActionResult> SeedData()
         {
             try
             {
                 // Kiểm tra xem đã có dữ liệu chưa
                 if (await _context.Users.AnyAsync())
                 {
                     return BadRequest(new { message = "Dữ liệu đã tồn tại" });
                 }

                 using var transaction = await _context.Database.BeginTransactionAsync();

                 try
                 {
                     // Tạo admin user
                     var adminUser = new User
                     {
                         Username = "admin",
                         Password = HashPassword("admin123"),
                         Email = "admin@thuybinhduong.com",
                         PhoneNumber = "0123456789",
                         Role = 1,
                         CreatedAt = DateTime.UtcNow
                     };
                     _context.Users.Add(adminUser);
                     await _context.SaveChangesAsync();

                     // Tạo khách hàng mẫu
                     var customerUser1 = new User
                     {
                         Username = "customer1",
                         Password = HashPassword("customer123"),
                         Email = "customer1@gmail.com",
                         PhoneNumber = "0987654321",
                         Role = 0,
                         CreatedAt = DateTime.UtcNow
                     };
                     _context.Users.Add(customerUser1);
                     await _context.SaveChangesAsync();

                     var customer1 = new Customer
                     {
                         UserId = customerUser1.UserId,
                         CustomerName = "Nguyễn Văn An",
                         Address = "123 Đường ABC, Quận 1, TP.HCM",
                         Gender = 0
                     };
                     _context.Customers.Add(customer1);

                     var customerUser2 = new User
                     {
                         Username = "customer2",
                         Password = HashPassword("customer123"),
                         Email = "customer2@gmail.com",
                         PhoneNumber = "0912345678",
                         Role = 0,
                         CreatedAt = DateTime.UtcNow
                     };
                     _context.Users.Add(customerUser2);
                     await _context.SaveChangesAsync();

                     var customer2 = new Customer
                     {
                         UserId = customerUser2.UserId,
                         CustomerName = "Trần Thị Bình",
                         Address = "456 Đường XYZ, Quận 2, TP.HCM",
                         Gender = 1
                     };
                     _context.Customers.Add(customer2);

                     await _context.SaveChangesAsync();
                     await transaction.CommitAsync();

                     return Ok(new { message = "Tạo dữ liệu mẫu thành công" });
                 }
                 catch
                 {
                     await transaction.RollbackAsync();
                     throw;
                 }
             }
             catch (Exception ex)
             {
                 _logger.LogError(ex, "Error seeding data");
                 return StatusCode(500, new { message = "Đã xảy ra lỗi khi tạo dữ liệu mẫu" });
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