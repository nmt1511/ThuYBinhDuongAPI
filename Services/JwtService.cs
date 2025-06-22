using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ThuYBinhDuongAPI.Models;

namespace ThuYBinhDuongAPI.Services;

/// <summary>
/// Dịch vụ quản lý JWT token cho hệ thống phòng khám thú y
/// </summary>
public class JwtService : IJwtService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<JwtService> _logger;

    public JwtService(IConfiguration configuration, ILogger<JwtService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Tạo JWT token cho người dùng
    /// </summary>
    /// <param name="user">Thông tin người dùng</param>
    /// <param name="roleName">Tên vai trò</param>
    /// <returns>JWT token string</returns>
    public string GenerateToken(User user, string roleName)
    {
        try
        {
            // Lấy cấu hình JWT từ appsettings
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var secretKey = jwtSettings["SecretKey"];
            var issuer = jwtSettings["Issuer"];
            var audience = jwtSettings["Audience"];
            var expiryMinutes = int.Parse(jwtSettings["ExpiryInMinutes"] ?? "60");

            // Tạo security key từ secret key
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            // Tạo claims (thông tin trong token)
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, roleName),
                new Claim("RoleId", user.Role.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.Iat, 
                    new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
            };

            // Tạo token
            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
                signingCredentials: credentials
            );

            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

            _logger.LogInformation("Token được tạo thành công cho người dùng: {Username}", user.Username);

            return tokenString;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi tạo JWT token cho người dùng: {UserId}", user.UserId);
            throw new InvalidOperationException("Không thể tạo JWT token");
        }
    }

    /// <summary>
    /// Lấy thông tin người dùng từ token
    /// </summary>
    /// <param name="token">JWT token</param>
    /// <returns>User ID nếu token hợp lệ</returns>
    public int? GetUserIdFromToken(string token)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var jsonToken = tokenHandler.ReadJwtToken(token);

            // Lấy user ID từ claims
            var userIdClaim = jsonToken.Claims.FirstOrDefault(x => x.Type == ClaimTypes.NameIdentifier);
            
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
            {
                return userId;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể lấy User ID từ token");
            return null;
        }
    }

    /// <summary>
    /// Kiểm tra token có hợp lệ không
    /// </summary>
    /// <param name="token">JWT token</param>
    /// <returns>True nếu token hợp lệ</returns>
    public bool ValidateToken(string token)
    {
        try
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var secretKey = jwtSettings["SecretKey"];
            var issuer = jwtSettings["Issuer"];
            var audience = jwtSettings["Audience"];

            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(secretKey);

            // Tham số validation
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = issuer,
                ValidateAudience = true,
                ValidAudience = audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero // Không cho phép sai lệch thời gian
            };

            // Validate token
            tokenHandler.ValidateToken(token, validationParameters, out SecurityToken validatedToken);

            return true;
        }
        catch (SecurityTokenExpiredException)
        {
            _logger.LogWarning("Token đã hết hạn");
            return false;
        }
        catch (SecurityTokenInvalidSignatureException)
        {
            _logger.LogWarning("Token có chữ ký không hợp lệ");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Token không hợp lệ");
            return false;
        }
    }
} 