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
                pet.Name = updatePetDto.Name;
                pet.Species = updatePetDto.Species;
                pet.Breed = updatePetDto.Breed;
                pet.BirthDate = updatePetDto.BirthDate;
                pet.ImageUrl = updatePetDto.ImageUrl;

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

        /// <summary>
        /// Lấy danh sách khách hàng (dành cho admin pet management)
        /// </summary>
        [HttpGet("admin/customers")]
        [AuthorizeRole(1)] // Chỉ admin
        public async Task<ActionResult<IEnumerable<object>>> GetCustomersForPetManagement([FromQuery] int page = 1, [FromQuery] int limit = 1000, [FromQuery] string? search = null)
        {
            try
            {
                var skip = (page - 1) * limit;
                var query = _context.Customers
                    .Include(c => c.User)
                    .AsQueryable();

                // Search filter
                if (!string.IsNullOrWhiteSpace(search))
                {
                    var searchTerm = search.ToLower().Trim();
                    query = query.Where(c => 
                        c.CustomerName.ToLower().Contains(searchTerm) ||
                        (c.User.Email != null && c.User.Email.ToLower().Contains(searchTerm)) ||
                        (c.User.PhoneNumber != null && c.User.PhoneNumber.Contains(searchTerm))
                    );
                }

                var customers = await query
                    .Select(c => new
                    {
                        CustomerId = c.CustomerId,
                        CustomerName = c.CustomerName,
                        Email = c.User.Email,
                        PhoneNumber = c.User.PhoneNumber,
                        Address = c.Address,
                        Gender = c.Gender,
                        UserId = c.UserId,
                        Username = c.User.Username,
                        Role = c.User.Role
                    })
                    .OrderBy(c => c.CustomerName)
                    .Skip(skip)
                    .Take(limit)
                    .ToListAsync();

                var totalCustomers = await query.CountAsync();

                _logger.LogInformation($"Admin retrieved {customers.Count} customers for pet management (page {page})");
                return Ok(new
                {
                    customers = customers,
                    pagination = new
                    {
                        page = page,
                        limit = limit,
                        total = totalCustomers,
                        totalPages = (int)Math.Ceiling((double)totalCustomers / limit)
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving customers for pet management");
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi lấy danh sách khách hàng" });
            }
        }

        /// <summary>
        /// Tạo dữ liệu mẫu cho Pet management (dành cho admin)
        /// </summary>
        [HttpPost("admin/seed-customers")]
        [AuthorizeRole(1)] // Chỉ admin
        public async Task<IActionResult> SeedCustomersForPetManagement()
        {
            try
            {
                // Kiểm tra xem đã có customers chưa
                var existingCustomers = await _context.Customers.CountAsync();
                if (existingCustomers > 0)
                {
                    return BadRequest(new { message = "Đã có dữ liệu khách hàng trong hệ thống" });
                }

                using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    // Tạo khách hàng mẫu 1
                    var customerUser1 = new User
                    {
                        Username = "customer1",
                        Password = "hashed_password_1", // In real implementation, hash this
                        Email = "nguyenvanan@gmail.com",
                        PhoneNumber = "0987654321",
                        Role = 0, // Customer role
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.Users.Add(customerUser1);
                    await _context.SaveChangesAsync();

                    var customer1 = new Customer
                    {
                        UserId = customerUser1.UserId,
                        CustomerName = "Nguyễn Văn An",
                        Address = "123 Đường Lê Lợi, Quận 1, TP.HCM",
                        Gender = 0 // Male
                    };
                    _context.Customers.Add(customer1);

                    // Tạo khách hàng mẫu 2
                    var customerUser2 = new User
                    {
                        Username = "customer2",
                        Password = "hashed_password_2", // In real implementation, hash this
                        Email = "tranthibinh@gmail.com",
                        PhoneNumber = "0912345678",
                        Role = 0, // Customer role
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.Users.Add(customerUser2);
                    await _context.SaveChangesAsync();

                    var customer2 = new Customer
                    {
                        UserId = customerUser2.UserId,
                        CustomerName = "Trần Thị Bình",
                        Address = "456 Đường Nguyễn Huệ, Quận 3, TP.HCM",
                        Gender = 1 // Female
                    };
                    _context.Customers.Add(customer2);

                    // Tạo khách hàng mẫu 3
                    var customerUser3 = new User
                    {
                        Username = "customer3",
                        Password = "hashed_password_3", // In real implementation, hash this
                        Email = "levanca@gmail.com",
                        PhoneNumber = "0903456789",
                        Role = 0, // Customer role
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.Users.Add(customerUser3);
                    await _context.SaveChangesAsync();

                    var customer3 = new Customer
                    {
                        UserId = customerUser3.UserId,
                        CustomerName = "Lê Văn Ca",
                        Address = "789 Đường Võ Văn Tần, Quận 5, TP.HCM",
                        Gender = 0 // Male
                    };
                    _context.Customers.Add(customer3);

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    _logger.LogInformation("Admin created seed customers for pet management");
                    return Ok(new { message = "Đã tạo dữ liệu khách hàng mẫu thành công", count = 3 });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    throw ex;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating seed customers for pet management");
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi tạo dữ liệu khách hàng mẫu" });
            }
        }

        #endregion
    }
} 