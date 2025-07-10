using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThuYBinhDuongAPI.Data.Dtos;
using ThuYBinhDuongAPI.Models;

namespace ThuYBinhDuongAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class MedicalHistoryController : ControllerBase
    {
        private readonly ThuybinhduongContext _context;
        private readonly ILogger<MedicalHistoryController> _logger;

        public MedicalHistoryController(ThuybinhduongContext context, ILogger<MedicalHistoryController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Lấy danh sách hồ sơ bệnh án (admin)
        /// </summary>
        [HttpGet("admin")]
        [AuthorizeRole(1)] // Chỉ admin
        public async Task<ActionResult> GetMedicalHistories(
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

                // Lọc theo petId nếu có
                if (petId.HasValue)
                {
                    query = query.Where(mh => mh.PetId == petId.Value);
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

                // Tìm kiếm theo từ khóa
                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    var search = searchTerm.Trim().ToLower();
                    query = query.Where(mh =>
                        (mh.Description != null && mh.Description.ToLower().Contains(search)) ||
                        (mh.Treatment != null && mh.Treatment.ToLower().Contains(search)) ||
                        (mh.Notes != null && mh.Notes.ToLower().Contains(search)) ||
                        mh.Pet.Name.ToLower().Contains(search) ||
                        mh.Pet.Customer.CustomerName.ToLower().Contains(search)
                    );
                }

                // Sắp xếp theo ngày gần nhất
                query = query.OrderByDescending(mh => mh.RecordDate);

                var total = await query.CountAsync();
                var totalPages = (int)Math.Ceiling((double)total / limit);
                var skip = (page - 1) * limit;

                var histories = await query
                    .Skip(skip)
                    .Take(limit)
                    .Select(mh => new
                    {
                        mh.HistoryId,
                        mh.PetId,
                        mh.RecordDate,
                        mh.Description,
                        mh.Treatment,
                        mh.Notes,
                        Pet = new
                        {
                            mh.Pet.PetId,
                            mh.Pet.Name,
                            Customer = new
                            {
                                mh.Pet.Customer.CustomerId,
                                mh.Pet.Customer.CustomerName
                            }
                        }
                    })
                    .ToListAsync();

                _logger.LogInformation("Retrieved {Count} medical histories (page {Page})", histories.Count, page);
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
                _logger.LogError(ex, "Error retrieving medical histories");
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi lấy danh sách hồ sơ bệnh án" });
            }
        }

        /// <summary>
        /// Lấy chi tiết một hồ sơ bệnh án (admin)
        /// </summary>
        [HttpGet("admin/{id}")]
        [AuthorizeRole(1)] // Chỉ admin
        public async Task<ActionResult> GetMedicalHistory(int id)
        {
            try
            {
                var history = await _context.MedicalHistories
                    .Include(mh => mh.Pet)
                        .ThenInclude(p => p.Customer)
                    .Where(mh => mh.HistoryId == id)
                    .Select(mh => new
                    {
                        mh.HistoryId,
                        mh.PetId,
                        mh.RecordDate,
                        mh.Description,
                        mh.Treatment,
                        mh.Notes,
                        Pet = new
                        {
                            mh.Pet.PetId,
                            mh.Pet.Name,
                            Customer = new
                            {
                                mh.Pet.Customer.CustomerId,
                                mh.Pet.Customer.CustomerName
                            }
                        }
                    })
                    .FirstOrDefaultAsync();

                if (history == null)
                {
                    return NotFound(new { message = "Không tìm thấy hồ sơ bệnh án" });
                }

                _logger.LogInformation("Retrieved medical history {HistoryId}", id);
                return Ok(history);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving medical history {HistoryId}", id);
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi lấy thông tin hồ sơ bệnh án" });
            }
        }

        /// <summary>
        /// Thêm hồ sơ bệnh án mới (admin)
        /// </summary>
        [HttpPost("admin")]
        [AuthorizeRole(1)] // Chỉ admin
        public async Task<ActionResult> CreateMedicalHistory([FromBody] MedicalHistoryDto createDto)
        {
            try
            {
                _logger.LogInformation("Creating medical history: {@MedicalHistoryDto}", createDto);

                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();
                    _logger.LogWarning("Validation errors: {@Errors}", errors);
                    return BadRequest(new { message = "Dữ liệu không hợp lệ", errors = errors });
                }

                // Kiểm tra thú cưng có tồn tại không
                var pet = await _context.Pets
                    .Include(p => p.Customer)
                    .FirstOrDefaultAsync(p => p.PetId == createDto.PetId);

                if (pet == null)
                {
                    _logger.LogWarning("Pet {PetId} not found", createDto.PetId);
                    return BadRequest(new { message = "Không tìm thấy thú cưng" });
                }

                var history = new MedicalHistory
                {
                    PetId = createDto.PetId,
                    RecordDate = createDto.RecordDate ?? DateTime.Now,
                    Description = createDto.Description?.Trim(),
                    Treatment = createDto.Treatment?.Trim(),
                    Notes = createDto.Notes?.Trim()
                };

                _context.MedicalHistories.Add(history);
                await _context.SaveChangesAsync();

                var response = new
                {
                    history.HistoryId,
                    history.PetId,
                    history.RecordDate,
                    history.Description,
                    history.Treatment,
                    history.Notes,
                    Pet = new
                    {
                        pet.PetId,
                        pet.Name,
                        Customer = new
                        {
                            pet.Customer.CustomerId,
                            pet.Customer.CustomerName
                        }
                    }
                };

                _logger.LogInformation("Created medical history {HistoryId} for pet {PetId}", history.HistoryId, history.PetId);
                return CreatedAtAction(nameof(GetMedicalHistory), new { id = history.HistoryId }, response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating medical history");
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi thêm hồ sơ bệnh án" });
            }
        }

        /// <summary>
        /// Cập nhật hồ sơ bệnh án (admin)
        /// </summary>
        [HttpPut("admin/{id}")]
        [AuthorizeRole(1)] // Chỉ admin
        public async Task<ActionResult> UpdateMedicalHistory(int id, [FromBody] MedicalHistoryDto updateDto)
        {
            try
            {
                _logger.LogInformation("Updating medical history {HistoryId}: {@MedicalHistoryDto}", id, updateDto);

                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();
                    _logger.LogWarning("Validation errors: {@Errors}", errors);
                    return BadRequest(new { message = "Dữ liệu không hợp lệ", errors = errors });
                }

                var history = await _context.MedicalHistories
                    .Include(mh => mh.Pet)
                        .ThenInclude(p => p.Customer)
                    .FirstOrDefaultAsync(mh => mh.HistoryId == id);

                if (history == null)
                {
                    return NotFound(new { message = "Không tìm thấy hồ sơ bệnh án" });
                }

                // Cập nhật thông tin
                history.RecordDate = updateDto.RecordDate ?? history.RecordDate;
                history.Description = updateDto.Description?.Trim() ?? history.Description;
                history.Treatment = updateDto.Treatment?.Trim() ?? history.Treatment;
                history.Notes = updateDto.Notes?.Trim() ?? history.Notes;

                await _context.SaveChangesAsync();

                var response = new
                {
                    history.HistoryId,
                    history.PetId,
                    history.RecordDate,
                    history.Description,
                    history.Treatment,
                    history.Notes,
                    Pet = new
                    {
                        history.Pet.PetId,
                        history.Pet.Name,
                        Customer = new
                        {
                            history.Pet.Customer.CustomerId,
                            history.Pet.Customer.CustomerName
                        }
                    }
                };

                _logger.LogInformation("Updated medical history {HistoryId}", id);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating medical history {HistoryId}", id);
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi cập nhật hồ sơ bệnh án" });
            }
        }

        /// <summary>
        /// Xóa hồ sơ bệnh án (admin)
        /// </summary>
        [HttpDelete("admin/{id}")]
        [AuthorizeRole(1)] // Chỉ admin
        public async Task<ActionResult> DeleteMedicalHistory(int id)
        {
            try
            {
                var history = await _context.MedicalHistories.FindAsync(id);
                if (history == null)
                {
                    return NotFound(new { message = "Không tìm thấy hồ sơ bệnh án" });
                }

                _context.MedicalHistories.Remove(history);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Deleted medical history {HistoryId}", id);
                return Ok(new { message = "Đã xóa hồ sơ bệnh án thành công" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting medical history {HistoryId}", id);
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi xóa hồ sơ bệnh án" });
            }
        }
    }
} 