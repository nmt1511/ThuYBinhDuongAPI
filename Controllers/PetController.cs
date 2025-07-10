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
                        Gender = p.Gender,
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
                    .Include(p => p.MedicalHistories)
                    .Where(p => p.PetId == id && p.CustomerId == customerId.Value)
                    .FirstOrDefaultAsync();

                if (pet == null)
                {
                    return NotFound(new { message = "Không tìm thấy thú cưng hoặc bạn không có quyền truy cập" });
                }

                var petDto = new PetResponseDto
                {
                    PetId = pet.PetId,
                    CustomerId = pet.CustomerId,
                    Name = pet.Name,
                    Species = pet.Species,
                    Breed = pet.Breed,
                    BirthDate = pet.BirthDate,
                    ImageUrl = pet.ImageUrl,
                    Gender = pet.Gender,
                    Age = pet.BirthDate.HasValue ? CalculateAge(pet.BirthDate.Value) : null,
                    CustomerName = pet.Customer.CustomerName,
                    MedicalHistories = pet.MedicalHistories?.OrderByDescending(mh => mh.RecordDate).Select(mh => new MedicalHistoryDto
                    {
                        HistoryId = mh.HistoryId,
                        PetId = mh.PetId,
                        RecordDate = mh.RecordDate,
                        Description = mh.Description,
                        Treatment = mh.Treatment,
                        Notes = mh.Notes
                    }).ToList()
                };

                _logger.LogInformation($"Customer {customerId} retrieved pet {id}");
                return Ok(petDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving pet {PetId}", id);
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi lấy thông tin thú cưng" });
            }
        }

        /// <summary>
        /// Lấy danh sách hồ sơ bệnh án của thú cưng (chỉ khách hàng sở hữu thú cưng đó mới xem được)
        /// </summary>
        [HttpGet("{id}/medical-history")]
        [AuthorizeRole(0)] // Chỉ khách hàng
        public async Task<ActionResult> GetMedicalHistory(int id, [FromQuery] int page = 1, [FromQuery] int limit = 5)
        {
            try
            {
                var customerId = await GetCurrentCustomerIdAsync();
                if (customerId == null)
                {
                    return BadRequest(new { message = "Không tìm thấy thông tin khách hàng" });
                }

                // Kiểm tra quyền truy cập thú cưng
                var pet = await _context.Pets
                    .Where(p => p.PetId == id && p.CustomerId == customerId.Value)
                    .FirstOrDefaultAsync();

                if (pet == null)
                {
                    return NotFound(new { message = "Không tìm thấy thú cưng hoặc bạn không có quyền truy cập" });
                }

                var query = _context.MedicalHistories
                    .Where(mh => mh.PetId == id)
                    .OrderByDescending(mh => mh.RecordDate);

                var total = await query.CountAsync();
                var totalPages = (int)Math.Ceiling((double)total / limit);
                var skip = (page - 1) * limit;

                var histories = await query
                    .Skip(skip)
                    .Take(limit)
                    .Select(mh => new MedicalHistoryDto
                    {
                        HistoryId = mh.HistoryId,
                        PetId = mh.PetId,
                        RecordDate = mh.RecordDate,
                        Description = mh.Description,
                        Treatment = mh.Treatment,
                        Notes = mh.Notes
                    })
                    .ToListAsync();

                return Ok(new
                {
                    histories = histories,
                    pagination = new
                    {
                        page = page,
                        limit = limit,
                        total = total,
                        totalPages = totalPages
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving medical history for pet {PetId}", id);
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi lấy hồ sơ bệnh án" });
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
                    ImageUrl = !string.IsNullOrWhiteSpace(createPetDto.ImageUrl) ? createPetDto.ImageUrl.Trim() : null,
                    Gender = !string.IsNullOrWhiteSpace(createPetDto.Gender) ? createPetDto.Gender.Trim() : null
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
                    Gender = pet.Gender,
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
                pet.Gender = !string.IsNullOrWhiteSpace(updatePetDto.Gender) ? updatePetDto.Gender.Trim() : null;

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
        /// Lấy danh sách hồ sơ bệnh án của thú cưng (dành cho admin)
        /// </summary>
        [HttpGet("admin/{id}/medical-history")]
        [AuthorizeRole(1)] // Chỉ admin
        public async Task<ActionResult> GetMedicalHistoryAdmin(int id, [FromQuery] int page = 1, [FromQuery] int limit = 10)
        {
            try
            {
                // Kiểm tra thú cưng có tồn tại không
                var pet = await _context.Pets
                    .Include(p => p.Customer)
                    .FirstOrDefaultAsync(p => p.PetId == id);

                if (pet == null)
                {
                    return NotFound(new { message = "Không tìm thấy thú cưng" });
                }

                var query = _context.MedicalHistories
                    .Where(mh => mh.PetId == id)
                    .OrderByDescending(mh => mh.RecordDate);

                var total = await query.CountAsync();
                var totalPages = (int)Math.Ceiling((double)total / limit);
                var skip = (page - 1) * limit;

                var histories = await query
                    .Skip(skip)
                    .Take(limit)
                    .Select(mh => new MedicalHistoryDto
                    {
                        HistoryId = mh.HistoryId,
                        PetId = mh.PetId,
                        RecordDate = mh.RecordDate,
                        Description = mh.Description,
                        Treatment = mh.Treatment,
                        Notes = mh.Notes
                    })
                    .ToListAsync();

                _logger.LogInformation($"Admin retrieved medical history for pet {id} (page {page})");
                return Ok(new
                {
                    petInfo = new
                    {
                        petId = pet.PetId,
                        petName = pet.Name,
                        customerName = pet.Customer.CustomerName,
                        customerId = pet.CustomerId
                    },
                    histories = histories,
                    pagination = new
                    {
                        page = page,
                        limit = limit,
                        total = total,
                        totalPages = totalPages
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving medical history for pet {PetId} for admin", id);
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi lấy hồ sơ bệnh án" });
            }
        }

        /// <summary>
        /// Thêm mới hồ sơ bệnh án (dành cho admin)
        /// </summary>
        [HttpPost("admin/{petId}/medical-history")]
        [AuthorizeRole(1)] // Chỉ admin
        public async Task<ActionResult> CreateMedicalHistoryAdmin(int petId, [FromBody] MedicalHistoryDto createDto)
        {
            try
            {
                // Kiểm tra thú cưng có tồn tại không
                var pet = await _context.Pets
                    .Include(p => p.Customer)
                    .FirstOrDefaultAsync(p => p.PetId == petId);

                if (pet == null)
                {
                    return NotFound(new { message = "Không tìm thấy thú cưng" });
                }

                var medicalHistory = new MedicalHistory
                {
                    PetId = petId,
                    RecordDate = createDto.RecordDate ?? DateTime.UtcNow,
                    Description = createDto.Description,
                    Treatment = createDto.Treatment,
                    Notes = createDto.Notes
                };

                _context.MedicalHistories.Add(medicalHistory);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Admin created medical history record {medicalHistory.HistoryId} for pet {petId}");
                return CreatedAtAction(nameof(GetMedicalHistoryAdmin), 
                    new { id = petId }, 
                    new { 
                        historyId = medicalHistory.HistoryId,
                        message = "Thêm hồ sơ bệnh án thành công" 
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating medical history for pet {PetId}", petId);
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi thêm hồ sơ bệnh án" });
            }
        }

        /// <summary>
        /// Cập nhật hồ sơ bệnh án (dành cho admin)
        /// </summary>
        [HttpPut("admin/medical-history/{historyId}")]
        [AuthorizeRole(1)] // Chỉ admin
        public async Task<ActionResult> UpdateMedicalHistoryAdmin(int historyId, [FromBody] MedicalHistoryDto updateDto)
        {
            try
            {
                var medicalHistory = await _context.MedicalHistories
                    .Include(mh => mh.Pet)
                    .FirstOrDefaultAsync(mh => mh.HistoryId == historyId);

                if (medicalHistory == null)
                {
                    return NotFound(new { message = "Không tìm thấy hồ sơ bệnh án" });
                }

                // Cập nhật thông tin
                medicalHistory.RecordDate = updateDto.RecordDate ?? medicalHistory.RecordDate;
                medicalHistory.Description = updateDto.Description;
                medicalHistory.Treatment = updateDto.Treatment;
                medicalHistory.Notes = updateDto.Notes;

                await _context.SaveChangesAsync();

                _logger.LogInformation($"Admin updated medical history record {historyId} for pet {medicalHistory.PetId}");
                return Ok(new { message = "Cập nhật hồ sơ bệnh án thành công" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating medical history {HistoryId}", historyId);
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi cập nhật hồ sơ bệnh án" });
            }
        }

        /// <summary>
        /// Xóa hồ sơ bệnh án (dành cho admin)
        /// </summary>
        [HttpDelete("admin/medical-history/{historyId}")]
        [AuthorizeRole(1)] // Chỉ admin
        public async Task<ActionResult> DeleteMedicalHistoryAdmin(int historyId)
        {
            try
            {
                var medicalHistory = await _context.MedicalHistories.FindAsync(historyId);
                if (medicalHistory == null)
                {
                    return NotFound(new { message = "Không tìm thấy hồ sơ bệnh án" });
                }

                _context.MedicalHistories.Remove(medicalHistory);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Admin deleted medical history record {historyId}");
                return Ok(new { message = "Xóa hồ sơ bệnh án thành công" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting medical history {HistoryId}", historyId);
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi xóa hồ sơ bệnh án" });
            }
        }

        /// <summary>
        /// Tìm kiếm hồ sơ bệnh án (dành cho admin)
        /// </summary>
        [HttpGet("admin/medical-history/search")]
        [AuthorizeRole(1)] // Chỉ admin
        public async Task<ActionResult> SearchMedicalHistoryAdmin(
            [FromQuery] string? searchTerm,
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            [FromQuery] int? petId,
            [FromQuery] int page = 1,
            [FromQuery] int limit = 10)
        {
            try
            {
                var query = _context.MedicalHistories
                    .Include(mh => mh.Pet)
                        .ThenInclude(p => p.Customer)
                    .AsQueryable();

                // Lọc theo từ khóa
                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    searchTerm = searchTerm.ToLower();
                    query = query.Where(mh =>
                        (mh.Description != null && mh.Description.ToLower().Contains(searchTerm)) ||
                        (mh.Treatment != null && mh.Treatment.ToLower().Contains(searchTerm)) ||
                        (mh.Notes != null && mh.Notes.ToLower().Contains(searchTerm)) ||
                        mh.Pet.Name.ToLower().Contains(searchTerm) ||
                        mh.Pet.Customer.CustomerName.ToLower().Contains(searchTerm));
                }

                // Lọc theo khoảng thời gian
                if (fromDate.HasValue)
                {
                    query = query.Where(mh => mh.RecordDate >= fromDate.Value);
                }
                if (toDate.HasValue)
                {
                    query = query.Where(mh => mh.RecordDate <= toDate.Value);
                }

                // Lọc theo thú cưng
                if (petId.HasValue)
                {
                    query = query.Where(mh => mh.PetId == petId.Value);
                }

                var total = await query.CountAsync();
                var totalPages = (int)Math.Ceiling((double)total / limit);
                var skip = (page - 1) * limit;

                var histories = await query
                    .OrderByDescending(mh => mh.RecordDate)
                    .Skip(skip)
                    .Take(limit)
                    .Select(mh => new
                    {
                        HistoryId = mh.HistoryId,
                        PetId = mh.PetId,
                        PetName = mh.Pet.Name,
                        CustomerName = mh.Pet.Customer.CustomerName,
                        CustomerId = mh.Pet.CustomerId,
                        RecordDate = mh.RecordDate,
                        Description = mh.Description,
                        Treatment = mh.Treatment,
                        Notes = mh.Notes
                    })
                    .ToListAsync();

                _logger.LogInformation($"Admin searched medical histories, found {total} results");
                return Ok(new
                {
                    histories = histories,
                    pagination = new
                    {
                        page = page,
                        limit = limit,
                        total = total,
                        totalPages = totalPages
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching medical histories");
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi tìm kiếm hồ sơ bệnh án" });
            }
        }

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
                        Gender = p.Gender,
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
                        Gender = p.Gender,
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
                    .Include(p => p.MedicalHistories)
                    .FirstOrDefaultAsync(p => p.PetId == id);

                if (pet == null)
                {
                    return NotFound(new { message = "Không tìm thấy thú cưng" });
                }

                var petDto = new PetResponseDto
                {
                    PetId = pet.PetId,
                    CustomerId = pet.CustomerId,
                    Name = pet.Name,
                    Species = pet.Species,
                    Breed = pet.Breed,
                    BirthDate = pet.BirthDate,
                    ImageUrl = pet.ImageUrl,
                    Gender = pet.Gender,
                    VaccinatedVaccines = pet.VaccinatedVaccines,
                    Age = pet.BirthDate.HasValue ? CalculateAge(pet.BirthDate.Value) : null,
                    CustomerName = pet.Customer.CustomerName,
                    MedicalHistories = pet.MedicalHistories?.OrderByDescending(mh => mh.RecordDate).Select(mh => new MedicalHistoryDto
                    {
                        HistoryId = mh.HistoryId,
                        PetId = mh.PetId,
                        RecordDate = mh.RecordDate,
                        Description = mh.Description,
                        Treatment = mh.Treatment,
                        Notes = mh.Notes
                    }).ToList()
                };

                _logger.LogInformation($"Admin retrieved pet {id}");
                return Ok(petDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving pet {PetId}", id);
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
                _logger.LogInformation("Admin creating pet with data: {@CreatePetDto} for customer {CustomerId}", createPetDto, customerId);

                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();
                    _logger.LogWarning("Validation errors: {@Errors}", errors);
                    return BadRequest(new { message = "Dữ liệu không hợp lệ", errors = errors });
                }

                // Kiểm tra customer có tồn tại không
                var customer = await _context.Customers.FindAsync(customerId);
                if (customer == null)
                {
                    _logger.LogWarning("Customer {CustomerId} not found", customerId);
                    return BadRequest(new { message = "Không tìm thấy khách hàng" });
                }

                // Kiểm tra tên thú cưng đã tồn tại cho khách hàng này chưa
                var existingPet = await _context.Pets
                    .AnyAsync(p => p.CustomerId == customerId && p.Name == createPetDto.Name);

                if (existingPet)
                {
                    _logger.LogWarning("Pet name already exists for customer {CustomerId}: {PetName}", customerId, createPetDto.Name);
                    return BadRequest(new { message = "Khách hàng đã có thú cưng với tên này" });
                }

                var pet = new Pet
                {
                    CustomerId = customerId,
                    Name = createPetDto.Name.Trim(),
                    Species = createPetDto.Species.Trim(),
                    Breed = !string.IsNullOrWhiteSpace(createPetDto.Breed) ? createPetDto.Breed.Trim() : null,
                    BirthDate = createPetDto.BirthDate ?? ParseBirthDate(createPetDto.BirthDateString),
                    ImageUrl = !string.IsNullOrWhiteSpace(createPetDto.ImageUrl) ? createPetDto.ImageUrl.Trim() : null,
                    Gender = !string.IsNullOrWhiteSpace(createPetDto.Gender) ? createPetDto.Gender.Trim() : null,
                    VaccinatedVaccines = !string.IsNullOrWhiteSpace(createPetDto.VaccinatedVaccines) ? createPetDto.VaccinatedVaccines.Trim() : null
                };

                _context.Pets.Add(pet);
                await _context.SaveChangesAsync();

                // Load thông tin customer để trả về
                await _context.Entry(pet)
                    .Reference(p => p.Customer)
                    .LoadAsync();

                var petDto = new PetResponseDto
                {
                    PetId = pet.PetId,
                    CustomerId = pet.CustomerId,
                    Name = pet.Name,
                    Species = pet.Species,
                    Breed = pet.Breed,
                    BirthDate = pet.BirthDate,
                    ImageUrl = pet.ImageUrl,
                    Gender = pet.Gender,
                    Age = pet.BirthDate.HasValue ? CalculateAge(pet.BirthDate.Value) : null,
                    CustomerName = pet.Customer.CustomerName
                };

                _logger.LogInformation("Admin created pet {PetId} for customer {CustomerId}", pet.PetId, customerId);
                return CreatedAtAction(nameof(GetPetAdmin), new { id = pet.PetId }, petDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating pet for customer {CustomerId}", customerId);
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
                pet.Gender = !string.IsNullOrWhiteSpace(updatePetDto.Gender) ? updatePetDto.Gender.Trim() : null;

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
                    .Include(c => c.Pets)  // Thêm Include Pets
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
                        Role = c.User.Role,
                        PetCount = c.Pets.Count,  // Số lượng thú cưng
                        AppointmentCount = c.Pets.SelectMany(p => p.Appointments).Count()  // Số lượng lịch hẹn
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

        #endregion
    }
} 