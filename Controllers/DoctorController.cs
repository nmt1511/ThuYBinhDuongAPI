using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThuYBinhDuongAPI.Data.Dtos;
using ThuYBinhDuongAPI.Models;

namespace ThuYBinhDuongAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DoctorController : ControllerBase
    {
        private readonly ThuybinhduongContext _context;
        private readonly ILogger<DoctorController> _logger;

        public DoctorController(ThuybinhduongContext context, ILogger<DoctorController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Lấy danh sách tất cả bác sĩ (cho dropdown đặt lịch)
        /// </summary>
        [HttpGet]
        [Authorize] // Cần đăng nhập để xem danh sách bác sĩ
        public async Task<ActionResult<IEnumerable<DoctorResponseDto>>> GetDoctors()
        {
            try
            {
                var doctors = await _context.Doctors
                    .OrderBy(d => d.FullName)
                    .ToListAsync();

                var doctorDtos = doctors.Select(d => new DoctorResponseDto
                {
                    DoctorId = d.DoctorId,
                    FullName = d.FullName,
                    Specialization = d.Specialization,
                    ExperienceYears = d.ExperienceYears,
                    Branch = d.Branch,
                    DisplayText = CreateDisplayText(d.FullName, d.Specialization, d.Branch)
                }).ToList();

                _logger.LogInformation($"Retrieved {doctorDtos.Count} doctors");
                return Ok(doctorDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving doctors");
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi lấy danh sách bác sĩ" });
            }
        }

        /// <summary>
        /// Lấy thông tin chi tiết một bác sĩ
        /// </summary>
        [HttpGet("{id}")]
        [Authorize]
        public async Task<ActionResult<DoctorResponseDto>> GetDoctor(int id)
        {
            try
            {
                var doctorEntity = await _context.Doctors
                    .Where(d => d.DoctorId == id)
                    .FirstOrDefaultAsync();

                if (doctorEntity == null)
                {
                    return NotFound(new { message = "Không tìm thấy bác sĩ" });
                }

                var doctor = new DoctorResponseDto
                {
                    DoctorId = doctorEntity.DoctorId,
                    FullName = doctorEntity.FullName,
                    Specialization = doctorEntity.Specialization,
                    ExperienceYears = doctorEntity.ExperienceYears,
                    Branch = doctorEntity.Branch,
                    DisplayText = CreateDisplayText(doctorEntity.FullName, doctorEntity.Specialization, doctorEntity.Branch)
                };



                _logger.LogInformation($"Retrieved doctor {id}");
                return Ok(doctor);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving doctor {DoctorId}", id);
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi lấy thông tin bác sĩ" });
            }
        }

        /// <summary>
        /// Tạo text hiển thị cho dropdown
        /// </summary>
        private static string CreateDisplayText(string fullName, string? specialization, string? branch)
        {
            var displayText = $"BS. {fullName}";
            
            if (!string.IsNullOrEmpty(specialization))
            {
                displayText += $" - {specialization}";
            }
            
            if (!string.IsNullOrEmpty(branch))
            {
                displayText += $" ({branch})";
            }
            
            return displayText;
        }

        #region Admin Methods

        /// <summary>
        /// Lấy tất cả bác sĩ (dành cho admin)
        /// </summary>
        [HttpGet("admin")]
        [AuthorizeRole(1)] // Chỉ admin
        public async Task<ActionResult<IEnumerable<DoctorResponseDto>>> GetAllDoctorsAdmin([FromQuery] int page = 1, [FromQuery] int limit = 10)
        {
            try
            {
                var skip = (page - 1) * limit;
                var doctors = await _context.Doctors
                    .OrderBy(d => d.FullName)
                    .Skip(skip)
                    .Take(limit)
                    .ToListAsync();

                var doctorDtos = doctors.Select(d => new DoctorResponseDto
                {
                    DoctorId = d.DoctorId,
                    FullName = d.FullName,
                    Specialization = d.Specialization,
                    ExperienceYears = d.ExperienceYears,
                    Branch = d.Branch,
                    DisplayText = CreateDisplayText(d.FullName, d.Specialization, d.Branch)
                }).ToList();

                var totalDoctors = await _context.Doctors.CountAsync();

                _logger.LogInformation($"Admin retrieved {doctorDtos.Count} doctors (page {page})");
                return Ok(new
                {
                    doctors = doctorDtos,
                    pagination = new
                    {
                        page = page,
                        limit = limit,
                        total = totalDoctors,
                        totalPages = (int)Math.Ceiling((double)totalDoctors / limit)
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all doctors for admin");
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi lấy danh sách bác sĩ" });
            }
        }

        /// <summary>
        /// Tìm kiếm bác sĩ (dành cho admin)
        /// </summary>
        [HttpGet("admin/search")]
        [AuthorizeRole(1)] // Chỉ admin
        public async Task<ActionResult<IEnumerable<DoctorResponseDto>>> SearchDoctorsAdmin([FromQuery] string query, [FromQuery] int page = 1, [FromQuery] int limit = 10)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query))
                {
                    return BadRequest(new { message = "Từ khóa tìm kiếm không được để trống" });
                }

                var skip = (page - 1) * limit;
                var searchQuery = query.ToLower().Trim();

                var doctors = await _context.Doctors
                    .Where(d => d.FullName.ToLower().Contains(searchQuery) ||
                               (d.Specialization != null && d.Specialization.ToLower().Contains(searchQuery)) ||
                               (d.Branch != null && d.Branch.ToLower().Contains(searchQuery)))
                    .OrderBy(d => d.FullName)
                    .Skip(skip)
                    .Take(limit)
                    .ToListAsync();

                var doctorDtos = doctors.Select(d => new DoctorResponseDto
                {
                    DoctorId = d.DoctorId,
                    FullName = d.FullName,
                    Specialization = d.Specialization,
                    ExperienceYears = d.ExperienceYears,
                    Branch = d.Branch,
                    DisplayText = CreateDisplayText(d.FullName, d.Specialization, d.Branch)
                }).ToList();

                var totalResults = await _context.Doctors
                    .Where(d => d.FullName.ToLower().Contains(searchQuery) ||
                               (d.Specialization != null && d.Specialization.ToLower().Contains(searchQuery)) ||
                               (d.Branch != null && d.Branch.ToLower().Contains(searchQuery)))
                    .CountAsync();

                _logger.LogInformation($"Admin searched doctors with query '{query}', found {totalResults} results");
                return Ok(new
                {
                    doctors = doctorDtos,
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
                _logger.LogError(ex, "Error searching doctors for admin");
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi tìm kiếm bác sĩ" });
            }
        }

        /// <summary>
        /// Lấy chi tiết bác sĩ (dành cho admin)
        /// </summary>
        [HttpGet("admin/{id}")]
        [AuthorizeRole(1)] // Chỉ admin
        public async Task<ActionResult<DoctorResponseDto>> GetDoctorAdmin(int id)
        {
            try
            {
                var doctorEntity = await _context.Doctors
                    .Where(d => d.DoctorId == id)
                    .FirstOrDefaultAsync();

                if (doctorEntity == null)
                {
                    return NotFound(new { message = "Không tìm thấy bác sĩ" });
                }

                var doctor = new DoctorResponseDto
                {
                    DoctorId = doctorEntity.DoctorId,
                    FullName = doctorEntity.FullName,
                    Specialization = doctorEntity.Specialization,
                    ExperienceYears = doctorEntity.ExperienceYears,
                    Branch = doctorEntity.Branch,
                    DisplayText = CreateDisplayText(doctorEntity.FullName, doctorEntity.Specialization, doctorEntity.Branch)
                };

                _logger.LogInformation($"Admin retrieved doctor {id}");
                return Ok(doctor);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving doctor {DoctorId} for admin", id);
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi lấy thông tin bác sĩ" });
            }
        }

        /// <summary>
        /// Tạo bác sĩ mới (dành cho admin)
        /// </summary>
        [HttpPost("admin")]
        [AuthorizeRole(1)] // Chỉ admin
        public async Task<ActionResult<DoctorResponseDto>> CreateDoctorAdmin([FromBody] CreateDoctorDto createDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Kiểm tra tên bác sĩ đã tồn tại chưa
                var existingDoctor = await _context.Doctors
                    .AnyAsync(d => d.FullName.ToLower() == createDto.FullName.ToLower());

                if (existingDoctor)
                {
                    return BadRequest(new { message = "Bác sĩ với tên này đã tồn tại" });
                }

                var doctor = new Doctor
                {
                    FullName = createDto.FullName,
                    Specialization = createDto.Specialization,
                    ExperienceYears = createDto.ExperienceYears,
                    Branch = createDto.Branch
                };

                _context.Doctors.Add(doctor);
                await _context.SaveChangesAsync();

                var response = new DoctorResponseDto
                {
                    DoctorId = doctor.DoctorId,
                    FullName = doctor.FullName,
                    Specialization = doctor.Specialization,
                    ExperienceYears = doctor.ExperienceYears,
                    Branch = doctor.Branch,
                    DisplayText = CreateDisplayText(doctor.FullName, doctor.Specialization, doctor.Branch)
                };

                _logger.LogInformation($"Admin created doctor {doctor.DoctorId} - {doctor.FullName}");
                return CreatedAtAction(nameof(GetDoctorAdmin), new { id = doctor.DoctorId }, response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating doctor for admin");
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi tạo bác sĩ" });
            }
        }

        /// <summary>
        /// Cập nhật bác sĩ (dành cho admin)
        /// </summary>
        [HttpPut("admin/{id}")]
        [AuthorizeRole(1)] // Chỉ admin
        public async Task<IActionResult> UpdateDoctorAdmin(int id, [FromBody] CreateDoctorDto updateDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var doctor = await _context.Doctors.FindAsync(id);
                if (doctor == null)
                {
                    return NotFound(new { message = "Không tìm thấy bác sĩ" });
                }

                // Kiểm tra tên bác sĩ đã tồn tại chưa (trừ bác sĩ hiện tại)
                var existingDoctor = await _context.Doctors
                    .AnyAsync(d => d.FullName.ToLower() == updateDto.FullName.ToLower() && d.DoctorId != id);

                if (existingDoctor)
                {
                    return BadRequest(new { message = "Bác sĩ với tên này đã tồn tại" });
                }

                // Cập nhật thông tin
                doctor.FullName = updateDto.FullName;
                doctor.Specialization = updateDto.Specialization;
                doctor.ExperienceYears = updateDto.ExperienceYears;
                doctor.Branch = updateDto.Branch;

                await _context.SaveChangesAsync();

                _logger.LogInformation($"Admin updated doctor {id}");
                return Ok(new { message = "Cập nhật thông tin bác sĩ thành công" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating doctor {DoctorId} for admin", id);
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi cập nhật thông tin bác sĩ" });
            }
        }

        /// <summary>
        /// Xóa bác sĩ (dành cho admin)
        /// </summary>
        [HttpDelete("admin/{id}")]
        [AuthorizeRole(1)] // Chỉ admin
        public async Task<IActionResult> DeleteDoctorAdmin(int id)
        {
            try
            {
                var doctor = await _context.Doctors.FindAsync(id);
                if (doctor == null)
                {
                    return NotFound(new { message = "Không tìm thấy bác sĩ" });
                }

                // Kiểm tra xem bác sĩ có đang được assign cho appointments không
                var hasActiveAppointments = await _context.Appointments
                    .AnyAsync(a => a.DoctorId == id && (a.Status == 0 || a.Status == 1)); // Chờ xác nhận hoặc đã xác nhận

                if (hasActiveAppointments)
                {
                    return BadRequest(new { message = "Không thể xóa bác sĩ đang có lịch hẹn hoạt động" });
                }

                _context.Doctors.Remove(doctor);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Admin deleted doctor {id} - {doctor.FullName}");
                return Ok(new { message = "Xóa bác sĩ thành công" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting doctor {DoctorId} for admin", id);
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi xóa bác sĩ" });
            }
        }

        #endregion
    }
} 