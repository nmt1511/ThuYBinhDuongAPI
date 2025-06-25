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
    }
} 