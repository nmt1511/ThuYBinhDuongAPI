using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

namespace ThuYBinhDuongAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UploadController : ControllerBase
    {
        private readonly ILogger<UploadController> _logger;
        private readonly Cloudinary _cloudinary;

        public UploadController(ILogger<UploadController> logger, Cloudinary cloudinary)
        {
            _logger = logger;
            _cloudinary = cloudinary;
        }

        /// <summary>
        /// Test endpoint
        /// </summary>
        [HttpGet("test")]
        [AllowAnonymous]
        public IActionResult Test()
        {
            return Ok(new { message = "Upload controller is working!", timestamp = DateTime.Now });
        }

        /// <summary>
        /// Upload hình ảnh lên Cloudinary
        /// </summary>
        [HttpPost("cloudinary")]
        [AllowAnonymous] // TODO: Remove this after testing
        public async Task<ActionResult<object>> UploadToCloudinary([FromForm] IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    return BadRequest(new { message = "Không có file được chọn" });
                }

                // Kiểm tra định dạng file
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
                
                if (!allowedExtensions.Contains(fileExtension))
                {
                    return BadRequest(new { message = "Chỉ chấp nhận file JPG, PNG, GIF" });
                }

                // Kiểm tra kích thước file (max 5MB)
                if (file.Length > 5 * 1024 * 1024)
                {
                    return BadRequest(new { message = "File không được vượt quá 5MB" });
                }

                using var stream = file.OpenReadStream();
                
                var uploadParams = new ImageUploadParams
                {
                    File = new FileDescription(file.FileName, stream),
                    Transformation = new Transformation().Width(800).Height(800).Crop("limit").Quality("auto"),
                    Folder = "thuybinhduong/chat"
                };

                var uploadResult = await _cloudinary.UploadAsync(uploadParams);

                if (uploadResult.Error != null)
                {
                    _logger.LogError($"Cloudinary upload error: {uploadResult.Error.Message}");
                    return StatusCode(500, new { message = "Lỗi khi upload ảnh lên Cloudinary" });
                }

                _logger.LogInformation($"Image uploaded successfully: {uploadResult.SecureUrl}");

                return Ok(new
                {
                    url = uploadResult.SecureUrl.ToString(),
                    publicId = uploadResult.PublicId,
                    width = uploadResult.Width,
                    height = uploadResult.Height,
                    format = uploadResult.Format,
                    message = "Upload thành công"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading image to Cloudinary");
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi upload ảnh" });
            }
        }

        /// <summary>
        /// Xóa hình ảnh từ Cloudinary
        /// </summary>
        [HttpDelete("cloudinary/{publicId}")]
        public async Task<IActionResult> DeleteFromCloudinary(string publicId)
        {
            try
            {
                var deletionParams = new DeletionParams(publicId);
                var result = await _cloudinary.DestroyAsync(deletionParams);

                if (result.Result == "ok")
                {
                    return Ok(new { message = "Xóa ảnh thành công" });
                }
                else
                {
                    return BadRequest(new { message = "Không thể xóa ảnh" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting image from Cloudinary");
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi xóa ảnh" });
            }
        }
    }
}

