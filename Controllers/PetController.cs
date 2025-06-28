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
                _logger.LogInformation("Creating pet with data: {@CreatePetDto}", createPetDto);

                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();
                    _logger.LogWarning("Validation errors: {@Errors}", errors);
                    return BadRequest(new { message = "Dữ liệu không hợp lệ", errors = errors });
                }

                var customerId = await GetCurrentCustomerIdAsync();
                if (customerId == null)
                {
                    _logger.LogWarning("Customer not found for user");
                    return BadRequest(new { message = "Không tìm thấy thông tin khách hàng" });
                }

                // Kiểm tra tên thú cưng đã tồn tại cho khách hàng này chưa
                var existingPet = await _context.Pets
                    .AnyAsync(p => p.CustomerId == customerId.Value && p.Name == createPetDto.Name);

                if (existingPet)
                {
                    _logger.LogWarning("Pet name already exists for customer {CustomerId}: {PetName}", customerId, createPetDto.Name);
                    return BadRequest(new { message = "Bạn đã có thú cưng với tên này" });
                }

                var pet = new Pet
                {
                    CustomerId = customerId.Value,
                    Name = createPetDto.Name.Trim(),
                    Species = createPetDto.Species.Trim(),
                    Breed = !string.IsNullOrWhiteSpace(createPetDto.Breed) ? createPetDto.Breed.Trim() : null,
                    BirthDate = createPetDto.BirthDate ?? ParseBirthDate(createPetDto.BirthDateString),
                    ImageUrl = !string.IsNullOrWhiteSpace(createPetDto.ImageUrl) ? createPetDto.ImageUrl.Trim() : null
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

                _logger.LogInformation("Successfully created pet {PetId} - {PetName} for customer {CustomerId}", pet.PetId, pet.Name, customerId);
                return CreatedAtAction(nameof(GetPet), new { id = pet.PetId }, response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating pet for customer. Data: {@CreatePetDto}", createPetDto);
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
                _logger.LogInformation("Updating pet {PetId} with data: {@UpdatePetDto}", id, updatePetDto);

                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();
                    _logger.LogWarning("Validation errors for pet {PetId}: {@Errors}", id, errors);
                    return BadRequest(new { message = "Dữ liệu không hợp lệ", errors = errors });
                }

                var customerId = await GetCurrentCustomerIdAsync();
                if (customerId == null)
                {
                    _logger.LogWarning("Customer not found for user");
                    return BadRequest(new { message = "Không tìm thấy thông tin khách hàng" });
                }

                var pet = await _context.Pets
                    .Where(p => p.PetId == id && p.CustomerId == customerId.Value)
                    .FirstOrDefaultAsync();

                if (pet == null)
                {
                    _logger.LogWarning("Pet {PetId} not found or customer {CustomerId} has no access", id, customerId);
                    return NotFound(new { message = "Không tìm thấy thú cưng hoặc bạn không có quyền chỉnh sửa" });
                }

                // Kiểm tra tên thú cưng mới có trùng với thú cưng khác không
                var existingPet = await _context.Pets
                    .AnyAsync(p => p.CustomerId == customerId.Value && p.Name == updatePetDto.Name && p.PetId != id);

                if (existingPet)
                {
                    _logger.LogWarning("Pet name already exists for customer {CustomerId}: {PetName}", customerId, updatePetDto.Name);
                    return BadRequest(new { message = "Bạn đã có thú cưng khác với tên này" });
                }

                // Cập nhật thông tin
                pet.Name = updatePetDto.Name.Trim();
                pet.Species = updatePetDto.Species.Trim();
                pet.Breed = !string.IsNullOrWhiteSpace(updatePetDto.Breed) ? updatePetDto.Breed.Trim() : null;
                pet.BirthDate = updatePetDto.BirthDate ?? ParseBirthDate(updatePetDto.BirthDateString);
                pet.ImageUrl = !string.IsNullOrWhiteSpace(updatePetDto.ImageUrl) ? updatePetDto.ImageUrl.Trim() : null;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Successfully updated pet {PetId} - {PetName} for customer {CustomerId}", id, pet.Name, customerId);
                return Ok(new { message = "Cập nhật thông tin thú cưng thành công" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating pet {PetId}. Data: {@UpdatePetDto}", id, updatePetDto);
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

        /// <summary>
        /// Parse ngày sinh từ string format (YYYY-MM-DD)
        /// </summary>
        private static DateOnly? ParseBirthDate(string? birthDateString)
        {
            if (string.IsNullOrWhiteSpace(birthDateString))
                return null;

            if (DateOnly.TryParse(birthDateString, out DateOnly birthDate))
                return birthDate;

            return null;
        }

        #region Admin Methods

        /// <summary>
        /// Lấy danh sách tất cả thú cưng (dành cho admin)
        /// </summary>
        [HttpGet("admin")]
        [AuthorizeRole(1)] // Chỉ admin
        public async Task<ActionResult<IEnumerable<PetResponseDto>>> GetAllPets([FromQuery] int page = 1, [FromQuery] int limit = 10, [FromQuery] int? customerId = null)
        {
            try
            {
                var skip = (page - 1) * limit;
                var query = _context.Pets
                    .Include(p => p.Customer)
                    .AsQueryable();

                // Filter by customer if specified
                if (customerId.HasValue)
                {
                    query = query.Where(p => p.CustomerId == customerId.Value);
                }

                var pets = await query
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
                    .OrderBy(p => p.CustomerName)
                    .ThenBy(p => p.Name)
                    .Skip(skip)
                    .Take(limit)
                    .ToListAsync();

                var totalPets = await query.CountAsync();

                _logger.LogInformation($"Admin retrieved {pets.Count} pets (page {page})");
                return Ok(new
                {
                    pets = pets,
                    pagination = new
                    {
                        page = page,
                        limit = limit,
                        total = totalPets,
                        totalPages = (int)Math.Ceiling((double)totalPets / limit)
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all pets for admin");
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi lấy danh sách thú cưng" });
            }
        }

        /// <summary>
        /// Tìm kiếm thú cưng (dành cho admin)
        /// </summary>
        [HttpGet("admin/search")]
        [AuthorizeRole(1)] // Chỉ admin
        public async Task<ActionResult<IEnumerable<PetResponseDto>>> SearchPets([FromQuery] string query, [FromQuery] int page = 1, [FromQuery] int limit = 10)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query))
                {
                    return BadRequest(new { message = "Từ khóa tìm kiếm không được để trống" });
                }

                var skip = (page - 1) * limit;
                var searchQuery = query.ToLower().Trim();

                var pets = await _context.Pets
                    .Include(p => p.Customer)
                    .Where(p => p.Name.ToLower().Contains(searchQuery) ||
                               p.Species.ToLower().Contains(searchQuery) ||
                               (p.Breed != null && p.Breed.ToLower().Contains(searchQuery)) ||
                               p.Customer.CustomerName.ToLower().Contains(searchQuery))
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
                    .OrderBy(p => p.CustomerName)
                    .ThenBy(p => p.Name)
                    .Skip(skip)
                    .Take(limit)
                    .ToListAsync();

                var totalResults = await _context.Pets
                    .Include(p => p.Customer)
                    .Where(p => p.Name.ToLower().Contains(searchQuery) ||
                               p.Species.ToLower().Contains(searchQuery) ||
                               (p.Breed != null && p.Breed.ToLower().Contains(searchQuery)) ||
                               p.Customer.CustomerName.ToLower().Contains(searchQuery))
                    .CountAsync();

                _logger.LogInformation($"Admin searched pets with query '{query}', found {totalResults} results");
                return Ok(new
                {
                    pets = pets,
                    searchQuery = query,
                    pagination = new
                    {
                        page = page,
                        limit = limit,
                        total = totalResults,
                        totalPages = (int)Math.Ceiling((double)totalResults / limit)
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching pets for admin");
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi tìm kiếm thú cưng" });
            }
        }

        /// <summary>
        /// Lấy thông tin chi tiết thú cưng (dành cho admin)
        /// </summary>
        [HttpGet("admin/{id}")]
        [AuthorizeRole(1)] // Chỉ admin
        public async Task<ActionResult<PetResponseDto>> GetPetAdmin(int id)
        {
            try
            {
                var pet = await _context.Pets
                    .Include(p => p.Customer)
                    .Where(p => p.PetId == id)
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
                    return NotFound(new { message = "Không tìm thấy thú cưng" });
                }

                _logger.LogInformation($"Admin retrieved pet {id}");
                return Ok(pet);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving pet {PetId} for admin", id);
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi lấy thông tin thú cưng" });
            }
        }

        /// <summary>
        /// Thêm thú cưng cho khách hàng (dành cho admin)
        /// </summary>
        [HttpPost("admin")]
        [AuthorizeRole(1)] // Chỉ admin
        public async Task<ActionResult<PetResponseDto>> CreatePetAdmin([FromBody] CreatePetDto createPetDto, [FromQuery] int customerId)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Kiểm tra customer có tồn tại không
                var customer = await _context.Customers.FindAsync(customerId);
                if (customer == null)
                {
                    return BadRequest(new { message = "Khách hàng không tồn tại" });
                }

                // Kiểm tra tên thú cưng đã tồn tại cho khách hàng này chưa
                var existingPet = await _context.Pets
                    .AnyAsync(p => p.CustomerId == customerId && p.Name == createPetDto.Name);

                if (existingPet)
                {
                    return BadRequest(new { message = "Khách hàng này đã có thú cưng với tên này" });
                }

                var pet = new Pet
                {
                    CustomerId = customerId,
                    Name = createPetDto.Name.Trim(),
                    Species = createPetDto.Species.Trim(),
                    Breed = !string.IsNullOrWhiteSpace(createPetDto.Breed) ? createPetDto.Breed.Trim() : null,
                    BirthDate = createPetDto.BirthDate ?? ParseBirthDate(createPetDto.BirthDateString),
                    ImageUrl = !string.IsNullOrWhiteSpace(createPetDto.ImageUrl) ? createPetDto.ImageUrl.Trim() : null
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

                _logger.LogInformation($"Admin created pet {pet.PetId} - {pet.Name} for customer {customerId}");
                return CreatedAtAction(nameof(GetPetAdmin), new { id = pet.PetId }, response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating pet for admin");
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi thêm thú cưng" });
            }
        }

        /// <summary>
        /// Cập nhật thông tin thú cưng (dành cho admin)
        /// </summary>
        [HttpPut("admin/{id}")]
        [AuthorizeRole(1)] // Chỉ admin
        public async Task<IActionResult> UpdatePetAdmin(int id, [FromBody] UpdatePetDto updatePetDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var pet = await _context.Pets.FindAsync(id);
                if (pet == null)
                {
                    return NotFound(new { message = "Không tìm thấy thú cưng" });
                }

                // Kiểm tra tên thú cưng đã tồn tại cho khách hàng này chưa (trừ thú cưng hiện tại)
                var existingPet = await _context.Pets
                    .AnyAsync(p => p.CustomerId == pet.CustomerId && p.Name == updatePetDto.Name && p.PetId != id);

                if (existingPet)
                {
                    return BadRequest(new { message = "Khách hàng này đã có thú cưng khác với tên này" });
                }

                // Cập nhật thông tin
                pet.Name = updatePetDto.Name.Trim();
                pet.Species = updatePetDto.Species.Trim();
                pet.Breed = !string.IsNullOrWhiteSpace(updatePetDto.Breed) ? updatePetDto.Breed.Trim() : null;
                pet.BirthDate = updatePetDto.BirthDate ?? ParseBirthDate(updatePetDto.BirthDateString);
                pet.ImageUrl = !string.IsNullOrWhiteSpace(updatePetDto.ImageUrl) ? updatePetDto.ImageUrl.Trim() : null;

                await _context.SaveChangesAsync();

                _logger.LogInformation($"Admin updated pet {id}");
                return Ok(new { message = "Cập nhật thông tin thú cưng thành công" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating pet {PetId} for admin", id);
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi cập nhật thông tin thú cưng" });
            }
        }

        /// <summary>
        /// Xóa thú cưng (dành cho admin)
        /// </summary>
        [HttpDelete("admin/{id}")]
        [AuthorizeRole(1)] // Chỉ admin
        public async Task<IActionResult> DeletePetAdmin(int id)
        {
            try
            {
                var pet = await _context.Pets.FindAsync(id);
                if (pet == null)
                {
                    return NotFound(new { message = "Không tìm thấy thú cưng" });
                }

                // Kiểm tra xem thú cưng có lịch hẹn nào chưa hoàn thành không
                var hasActiveAppointments = await _context.Appointments
                    .AnyAsync(a => a.PetId == id && (a.Status == 0 || a.Status == 1)); // Chờ xác nhận hoặc đã xác nhận

                if (hasActiveAppointments)
                {
                    return BadRequest(new { message = "Không thể xóa thú cưng có lịch hẹn đang hoạt động" });
                }

                _context.Pets.Remove(pet);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Admin deleted pet {id} - {pet.Name}");
                return Ok(new { message = "Xóa thú cưng thành công" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting pet {PetId} for admin", id);
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi xóa thú cưng" });
            }
        }

        #endregion
    }
} 