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
    public class PetController : ControllerBase
    {
        private readonly ThuybinhduongContext _context;
        private readonly IJwtService _jwtService;
        private readonly ILogger<PetController> _logger;

        public PetController(ThuybinhduongContext context, IJwtService jwtService, ILogger<PetController> logger)
        {
            _context = context;
            _jwtService = jwtService;
            _logger = logger;
        }

        /// <summary>
        /// Lấy danh sách thú cưng của khách hàng hiện tại
        /// </summary>
        [HttpGet]
        [AuthorizeRole(0)] // Chỉ khách hàng
        public async Task<ActionResult<IEnumerable<PetResponseDto>>> GetMyPets()
        {
            try
            {
                var customerId = await GetCurrentCustomerIdAsync();
                if (customerId == null)
                {
                    return BadRequest(new { message = "Không tìm thấy thông tin khách hàng" });
                }

                var pets = await _context.Pets
                    .Include(p => p.Customer)
                    .Where(p => p.CustomerId == customerId.Value)
                    .Select(p => new PetResponseDto
                    {
                        PetId = p.PetId,
                        CustomerId = p.CustomerId,
                        Name = p.Name,
                        Species = p.Species,
                        Breed = p.Breed,
                        BirthDate = p.BirthDate,
                        ImageUrl = p.ImageUrl,
                        Age = p.BirthDate.HasValue ? CalculateAge(p.BirthDate.Value) : null,
                        CustomerName = p.Customer.CustomerName
                    })
                    .OrderBy(p => p.Name)
                    .ToListAsync();

                _logger.LogInformation($"Customer {customerId} retrieved {pets.Count} pets");
                return Ok(pets);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving pets");
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi lấy danh sách thú cưng" });
            }
        }

        /// <summary>
        /// Lấy thông tin chi tiết một thú cưng
        /// </summary>
        [HttpGet("{id}")]
        [AuthorizeRole(0)] // Chỉ khách hàng
        public async Task<ActionResult<PetResponseDto>> GetPet(int id)
        {
            try
            {
                var customerId = await GetCurrentCustomerIdAsync();
                if (customerId == null)
                {
                    return BadRequest(new { message = "Không tìm thấy thông tin khách hàng" });
                }

                var pet = await _context.Pets
                    .Include(p => p.Customer)
                    .Where(p => p.PetId == id && p.CustomerId == customerId.Value)
                    .Select(p => new PetResponseDto
                    {
                        PetId = p.PetId,
                        CustomerId = p.CustomerId,
                        Name = p.Name,
                        Species = p.Species,
                        Breed = p.Breed,
                        BirthDate = p.BirthDate,
                        ImageUrl = p.ImageUrl,
                        Age = p.BirthDate.HasValue ? CalculateAge(p.BirthDate.Value) : null,
                        CustomerName = p.Customer.CustomerName
                    })
                    .FirstOrDefaultAsync();

                if (pet == null)
                {
                    return NotFound(new { message = "Không tìm thấy thú cưng hoặc bạn không có quyền truy cập" });
                }

                _logger.LogInformation($"Customer {customerId} retrieved pet {id}");
                return Ok(pet);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving pet {PetId}", id);
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi lấy thông tin thú cưng" });
            }
        }

        /// <summary>
        /// Thêm thú cưng mới cho khách hàng hiện tại
        /// </summary>
        [HttpPost]
        [AuthorizeRole(0)] // Chỉ khách hàng
        public async Task<ActionResult<PetResponseDto>> CreatePet([FromBody] CreatePetDto createPetDto)
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

                // Kiểm tra tên thú cưng đã tồn tại cho khách hàng này chưa
                var existingPet = await _context.Pets
                    .AnyAsync(p => p.CustomerId == customerId.Value && p.Name == createPetDto.Name);

                if (existingPet)
                {
                    return BadRequest(new { message = "Bạn đã có thú cưng với tên này" });
                }

                var pet = new Pet
                {
                    CustomerId = customerId.Value,
                    Name = createPetDto.Name,
                    Species = createPetDto.Species,
                    Breed = createPetDto.Breed,
                    BirthDate = createPetDto.BirthDate,
                    ImageUrl = createPetDto.ImageUrl
                };

                _context.Pets.Add(pet);
                await _context.SaveChangesAsync();

                // Load thông tin customer để trả về
                await _context.Entry(pet)
                    .Reference(p => p.Customer)
                    .LoadAsync();

                var response = new PetResponseDto
                {
                    PetId = pet.PetId,
                    CustomerId = pet.CustomerId,
                    Name = pet.Name,
                    Species = pet.Species,
                    Breed = pet.Breed,
                    BirthDate = pet.BirthDate,
                    ImageUrl = pet.ImageUrl,
                    Age = pet.BirthDate.HasValue ? CalculateAge(pet.BirthDate.Value) : null,
                    CustomerName = pet.Customer.CustomerName
                };

                _logger.LogInformation($"Customer {customerId} created pet {pet.PetId} - {pet.Name}");
                return CreatedAtAction(nameof(GetPet), new { id = pet.PetId }, response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating pet");
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi thêm thú cưng" });
            }
        }

        /// <summary>
        /// Cập nhật thông tin thú cưng
        /// </summary>
        [HttpPut("{id}")]
        [AuthorizeRole(0)] // Chỉ khách hàng
        public async Task<IActionResult> UpdatePet(int id, [FromBody] UpdatePetDto updatePetDto)
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

                var pet = await _context.Pets
                    .Where(p => p.PetId == id && p.CustomerId == customerId.Value)
                    .FirstOrDefaultAsync();

                if (pet == null)
                {
                    return NotFound(new { message = "Không tìm thấy thú cưng hoặc bạn không có quyền chỉnh sửa" });
                }

                // Kiểm tra tên thú cưng mới có trùng với thú cưng khác không
                var existingPet = await _context.Pets
                    .AnyAsync(p => p.CustomerId == customerId.Value && p.Name == updatePetDto.Name && p.PetId != id);

                if (existingPet)
                {
                    return BadRequest(new { message = "Bạn đã có thú cưng khác với tên này" });
                }

                // Cập nhật thông tin
                pet.Name = updatePetDto.Name;
                pet.Species = updatePetDto.Species;
                pet.Breed = updatePetDto.Breed;
                pet.BirthDate = updatePetDto.BirthDate;
                pet.ImageUrl = updatePetDto.ImageUrl;

                await _context.SaveChangesAsync();

                _logger.LogInformation($"Customer {customerId} updated pet {id} - {pet.Name}");
                return Ok(new { message = "Cập nhật thông tin thú cưng thành công" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating pet {PetId}", id);
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi cập nhật thông tin thú cưng" });
            }
        }

        /// <summary>
        /// Xóa thú cưng
        /// </summary>
        [HttpDelete("{id}")]
        [AuthorizeRole(0)] // Chỉ khách hàng
        public async Task<IActionResult> DeletePet(int id)
        {
            try
            {
                var customerId = await GetCurrentCustomerIdAsync();
                if (customerId == null)
                {
                    return BadRequest(new { message = "Không tìm thấy thông tin khách hàng" });
                }

                var pet = await _context.Pets
                    .Where(p => p.PetId == id && p.CustomerId == customerId.Value)
                    .FirstOrDefaultAsync();

                if (pet == null)
                {
                    return NotFound(new { message = "Không tìm thấy thú cưng hoặc bạn không có quyền xóa" });
                }

                // Kiểm tra xem thú cưng có lịch hẹn đang chờ hoặc đã xác nhận không
                var hasActiveAppointments = await _context.Appointments
                    .AnyAsync(a => a.PetId == id && (a.Status == 0 || a.Status == 1));

                if (hasActiveAppointments)
                {
                    return BadRequest(new { message = "Không thể xóa thú cưng có lịch hẹn đang chờ hoặc đã xác nhận" });
                }

                _context.Pets.Remove(pet);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Customer {customerId} deleted pet {id} - {pet.Name}");
                return Ok(new { message = "Xóa thú cưng thành công" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting pet {PetId}", id);
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi xóa thú cưng" });
            }
        }

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
        /// Tính tuổi từ ngày sinh
        /// </summary>
        private static int CalculateAge(DateOnly birthDate)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var age = today.Year - birthDate.Year;
            
            if (birthDate > today.AddYears(-age))
            {
                age--;
            }
            
            return age;
        }
    }
} 