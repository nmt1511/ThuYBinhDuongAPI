using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using ThuYBinhDuongAPI.Data.Dtos;
using ThuYBinhDuongAPI.Models;
using ThuYBinhDuongAPI.Services;

namespace ThuYBinhDuongAPI.Controllers;

/// <summary>
/// Controller quản lý người dùng - đăng ký và đăng nhập
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class UserController : ControllerBase
{
    private readonly ThuybinhduongContext _context;
    private readonly ILogger<UserController> _logger;
    private readonly IJwtService _jwtService;

    public UserController(ThuybinhduongContext context, ILogger<UserController> logger, IJwtService jwtService)
    {
        _context = context;
        _logger = logger;
        _jwtService = jwtService;
    }

    /// <summary>
    /// Đăng ký tài khoản người dùng mới
    /// </summary>
    /// <param name="registerDto">Thông tin đăng ký</param>
    /// <returns>Thông tin người dùng sau khi đăng ký thành công</returns>
    [HttpPost("register")]
    public async Task<ActionResult<UserResponseDto>> Register(RegisterDto registerDto)
    {
        try
        {
            // Kiểm tra tính hợp lệ của dữ liệu đầu vào
            if (!ModelState.IsValid)
            {
                return BadRequest("Dữ liệu đầu vào không hợp lệ");
            }

            // Kiểm tra tên đăng nhập đã tồn tại chưa
            var existingUserByUsername = await _context.Users
                .FirstOrDefaultAsync(u => u.Username == registerDto.Username);
            if (existingUserByUsername != null)
            {
                return Conflict("Tên đăng nhập đã tồn tại");
            }

            // Kiểm tra email đã tồn tại chưa
            var existingUserByEmail = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == registerDto.Email);
            if (existingUserByEmail != null)
            {
                return Conflict("Email đã được sử dụng");
            }

            // Kiểm tra vai trò có tồn tại không
            var role = await _context.Roles.FindAsync(registerDto.Role);
            if (role == null)
            {
                return BadRequest("Vai trò không hợp lệ");
            }

            // Mã hóa mật khẩu
            var hashedPassword = HashPassword(registerDto.Password);

            // Tạo người dùng mới
            var newUser = new User
            {
                Username = registerDto.Username,
                Password = hashedPassword,
                Email = registerDto.Email,
                PhoneNumber = registerDto.PhoneNumber,
                Role = registerDto.Role,
                CreatedAt = DateTime.Now
            };

            // Thêm vào cơ sở dữ liệu
            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Đăng ký tài khoản thành công cho người dùng: {Username}", registerDto.Username);

            // Tạo JWT token cho người dùng mới
            var token = _jwtService.GenerateToken(newUser, role.Name);

            // Trả về thông tin người dùng (không bao gồm mật khẩu) kèm JWT token
            var userResponse = new UserResponseDto
            {
                UserId = newUser.UserId,
                Username = newUser.Username,
                Email = newUser.Email,
                PhoneNumber = newUser.PhoneNumber,
                Role = newUser.Role,
                RoleName = role.Name,
                CreatedAt = newUser.CreatedAt,
                Token = token
            };

            return CreatedAtAction(nameof(GetUserById), new { id = newUser.UserId }, userResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi đăng ký tài khoản cho người dùng: {Username}", registerDto.Username);
            return StatusCode(500, "Có lỗi xảy ra trong quá trình đăng ký. Vui lòng thử lại sau.");
        }
    }

    /// <summary>
    /// Đăng nhập vào hệ thống
    /// </summary>
    /// <param name="loginDto">Thông tin đăng nhập</param>
    /// <returns>Thông tin người dùng sau khi đăng nhập thành công</returns>
    [HttpPost("login")]
    public async Task<ActionResult<UserResponseDto>> Login(LoginDto loginDto)
    {
        try
        {
            // Kiểm tra tính hợp lệ của dữ liệu đầu vào
            if (!ModelState.IsValid)
            {
                return BadRequest("Dữ liệu đầu vào không hợp lệ");
            }

            // Tìm người dùng theo tên đăng nhập
            var user = await _context.Users
                .Include(u => u.RoleNavigation)
                .FirstOrDefaultAsync(u => u.Username == loginDto.Username);

            if (user == null)
            {
                return Unauthorized("Tên đăng nhập hoặc mật khẩu không chính xác");
            }

            // Kiểm tra mật khẩu
            if (!VerifyPassword(loginDto.Password, user.Password))
            {
                return Unauthorized("Tên đăng nhập hoặc mật khẩu không chính xác");
            }

            _logger.LogInformation("Đăng nhập thành công cho người dùng: {Username}", loginDto.Username);

            // Tạo JWT token cho người dùng
            var token = _jwtService.GenerateToken(user, user.RoleNavigation.Name);

            // Trả về thông tin người dùng (không bao gồm mật khẩu) kèm JWT token
            var userResponse = new UserResponseDto
            {
                UserId = user.UserId,
                Username = user.Username,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                Role = user.Role,
                RoleName = user.RoleNavigation.Name,
                CreatedAt = user.CreatedAt,
                Token = token
            };

            return Ok(userResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi đăng nhập cho người dùng: {Username}", loginDto.Username);
            return StatusCode(500, "Có lỗi xảy ra trong quá trình đăng nhập. Vui lòng thử lại sau.");
        }
    }

    /// <summary>
    /// Lấy thông tin người dùng theo ID
    /// </summary>
    /// <param name="id">ID người dùng</param>
    /// <returns>Thông tin người dùng</returns>
    [HttpGet("{id}")]
    public async Task<ActionResult<UserResponseDto>> GetUserById(int id)
    {
        try
        {
            var user = await _context.Users
                .Include(u => u.RoleNavigation)
                .FirstOrDefaultAsync(u => u.UserId == id);

            if (user == null)
            {
                return NotFound("Không tìm thấy người dùng");
            }

            var userResponse = new UserResponseDto
            {
                UserId = user.UserId,
                Username = user.Username,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                Role = user.Role,
                RoleName = user.RoleNavigation.Name,
                CreatedAt = user.CreatedAt
            };

            return Ok(userResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi lấy thông tin người dùng ID: {UserId}", id);
            return StatusCode(500, "Có lỗi xảy ra khi lấy thông tin người dùng.");
        }
    }

    /// <summary>
    /// Mã hóa mật khẩu sử dụng SHA256
    /// </summary>
    /// <param name="password">Mật khẩu gốc</param>
    /// <returns>Mật khẩu đã được mã hóa</returns>
    private string HashPassword(string password)
    {
        using (var sha256 = SHA256.Create())
        {
            // Chuyển mật khẩu thành byte array
            var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            
            // Chuyển thành chuỗi hex
            return Convert.ToHexString(hashedBytes);
        }
    }

    /// <summary>
    /// Đăng xuất khỏi hệ thống (invalidate token)
    /// </summary>
    /// <returns>Thông báo đăng xuất thành công</returns>
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        try
        {
            // Lấy thông tin người dùng từ token
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            var username = User.FindFirst(ClaimTypes.Name)?.Value;

            if (userIdClaim != null)
            {
                _logger.LogInformation("Người dùng đăng xuất: {Username}", username);
                
                // TODO: Trong tương lai có thể implement token blacklist để invalidate token
                // Hiện tại chỉ log việc đăng xuất
                
                return Ok(new { message = "Đăng xuất thành công" });
            }

            return BadRequest("Token không hợp lệ");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi đăng xuất");
            return StatusCode(500, "Có lỗi xảy ra khi đăng xuất");
        }
    }

    /// <summary>
    /// Lấy thông tin profile của người dùng hiện tại
    /// </summary>
    /// <returns>Thông tin profile</returns>
    [HttpGet("profile")]
    [Authorize]
    public async Task<ActionResult<UserResponseDto>> GetProfile()
    {
        try
        {
            // Lấy User ID từ JWT token
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return Unauthorized("Token không hợp lệ");
            }

            var user = await _context.Users
                .Include(u => u.RoleNavigation)
                .FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null)
            {
                return NotFound("Không tìm thấy thông tin người dùng");
            }

            var userResponse = new UserResponseDto
            {
                UserId = user.UserId,
                Username = user.Username,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                Role = user.Role,
                RoleName = user.RoleNavigation.Name,
                CreatedAt = user.CreatedAt
            };

            return Ok(userResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi lấy thông tin profile");
            return StatusCode(500, "Có lỗi xảy ra khi lấy thông tin profile");
        }
    }

    /// <summary>
    /// Refresh JWT token
    /// </summary>
    /// <returns>Token mới</returns>
    [HttpPost("refresh-token")]
    [Authorize]
    public async Task<ActionResult<UserResponseDto>> RefreshToken()
    {
        try
        {
            // Lấy User ID từ JWT token hiện tại
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return Unauthorized("Token không hợp lệ");
            }

            var user = await _context.Users
                .Include(u => u.RoleNavigation)
                .FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null)
            {
                return NotFound("Không tìm thấy người dùng");
            }

            // Tạo token mới
            var newToken = _jwtService.GenerateToken(user, user.RoleNavigation.Name);

            _logger.LogInformation("Token được refresh cho người dùng: {Username}", user.Username);

            var userResponse = new UserResponseDto
            {
                UserId = user.UserId,
                Username = user.Username,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                Role = user.Role,
                RoleName = user.RoleNavigation.Name,
                CreatedAt = user.CreatedAt,
                Token = newToken
            };

            return Ok(userResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi refresh token");
            return StatusCode(500, "Có lỗi xảy ra khi refresh token");
        }
    }

    /// <summary>
    /// Xác minh mật khẩu
    /// </summary>
    /// <param name="password">Mật khẩu người dùng nhập vào</param>
    /// <param name="hashedPassword">Mật khẩu đã mã hóa trong database</param>
    /// <returns>True nếu mật khẩu khớp</returns>
    private bool VerifyPassword(string password, string hashedPassword)
    {
        // Mã hóa mật khẩu người dùng nhập vào
        var hashedInputPassword = HashPassword(password);
        
        // So sánh với mật khẩu trong database
        return hashedInputPassword.Equals(hashedPassword, StringComparison.OrdinalIgnoreCase);
    }
} 