using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThuYBinhDuongAPI.Data.Dtos;
using ThuYBinhDuongAPI.Models;

namespace ThuYBinhDuongAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class NewsController : ControllerBase
    {
        private readonly ThuybinhduongContext _context;
        private readonly ILogger<NewsController> _logger;

        public NewsController(ThuybinhduongContext context, ILogger<NewsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Lấy danh sách tin tức công khai (cho tất cả người dùng)
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<NewsResponseDto>>> GetPublicNews([FromQuery] int page = 1, [FromQuery] int limit = 10)
        {
            try
            {
                var skip = (page - 1) * limit;
                var newsQuery = await _context.News
                    .OrderByDescending(n => n.CreatedAt)
                    .Skip(skip)
                    .Take(limit)
                    .ToListAsync();

                var news = newsQuery.Select(n => new NewsResponseDto
                {
                    NewsId = n.NewsId,
                    Title = n.Title,
                    Content = n.Content,
                    ImageUrl = n.ImageUrl,
                    Tags = n.Tags,
                    CreatedAt = n.CreatedAt,
                    Summary = !string.IsNullOrEmpty(n.Content) && n.Content.Length > 200 ? 
                             n.Content.Substring(0, 200) + "..." : n.Content,
                    TagList = !string.IsNullOrEmpty(n.Tags) ? 
                             n.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                  .Select(t => t.Trim()).ToList() : new List<string>()
                }).ToList();

                var totalNews = await _context.News.CountAsync();

                return Ok(new
                {
                    news = news,
                    pagination = new
                    {
                        page = page,
                        limit = limit,
                        total = totalNews,
                        totalPages = (int)Math.Ceiling((double)totalNews / limit)
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving public news");
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi lấy danh sách tin tức" });
            }
        }

        /// <summary>
        /// Tìm kiếm tin tức công khai
        /// </summary>
        [HttpGet("search")]
        public async Task<ActionResult<IEnumerable<NewsResponseDto>>> SearchPublicNews([FromQuery] string query, [FromQuery] int page = 1, [FromQuery] int limit = 10)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query))
                {
                    return BadRequest(new { message = "Từ khóa tìm kiếm không được để trống" });
                }

                var skip = (page - 1) * limit;
                var searchQuery = query.ToLower().Trim();

                var newsQuery = await _context.News
                    .Where(n => n.Title.ToLower().Contains(searchQuery) ||
                               (n.Content != null && n.Content.ToLower().Contains(searchQuery)) ||
                               (n.Tags != null && n.Tags.ToLower().Contains(searchQuery)))
                    .OrderByDescending(n => n.CreatedAt)
                    .Skip(skip)
                    .Take(limit)
                    .ToListAsync();

                var news = newsQuery.Select(n => new NewsResponseDto
                {
                    NewsId = n.NewsId,
                    Title = n.Title,
                    Content = n.Content,
                    ImageUrl = n.ImageUrl,
                    Tags = n.Tags,
                    CreatedAt = n.CreatedAt,
                    Summary = !string.IsNullOrEmpty(n.Content) && n.Content.Length > 200 ? 
                             n.Content.Substring(0, 200) + "..." : n.Content,
                    TagList = !string.IsNullOrEmpty(n.Tags) ? 
                             n.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                  .Select(t => t.Trim()).ToList() : new List<string>()
                }).ToList();

                var totalResults = await _context.News
                    .Where(n => n.Title.ToLower().Contains(searchQuery) ||
                               (n.Content != null && n.Content.ToLower().Contains(searchQuery)) ||
                               (n.Tags != null && n.Tags.ToLower().Contains(searchQuery)))
                    .CountAsync();

                return Ok(new
                {
                    news = news,
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
                _logger.LogError(ex, "Error searching public news");
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi tìm kiếm tin tức" });
            }
        }

        /// <summary>
        /// Lấy chi tiết tin tức theo ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<NewsResponseDto>> GetNewsById(int id)
        {
            try
            {
                var newsItem = await _context.News
                    .Where(n => n.NewsId == id)
                    .FirstOrDefaultAsync();

                if (newsItem == null)
                {
                    return NotFound(new { message = "Không tìm thấy tin tức" });
                }

                var news = new NewsResponseDto
                {
                    NewsId = newsItem.NewsId,
                    Title = newsItem.Title,
                    Content = newsItem.Content,
                    ImageUrl = newsItem.ImageUrl,
                    Tags = newsItem.Tags,
                    CreatedAt = newsItem.CreatedAt,
                    Summary = !string.IsNullOrEmpty(newsItem.Content) && newsItem.Content.Length > 200 ? 
                             newsItem.Content.Substring(0, 200) + "..." : newsItem.Content,
                    TagList = !string.IsNullOrEmpty(newsItem.Tags) ? 
                             newsItem.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                  .Select(t => t.Trim()).ToList() : new List<string>()
                };

                return Ok(news);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving news {NewsId}", id);
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi lấy thông tin tin tức" });
            }
        }

        #region Admin Methods

        /// <summary>
        /// Lấy tất cả tin tức (dành cho admin)
        /// </summary>
        [HttpGet("admin")]
        [AuthorizeRole(1)] // Chỉ admin
        public async Task<ActionResult<IEnumerable<NewsResponseDto>>> GetAllNewsAdmin([FromQuery] int page = 1, [FromQuery] int limit = 10)
        {
            try
            {
                var skip = (page - 1) * limit;
                var newsQuery = await _context.News
                    .OrderByDescending(n => n.CreatedAt)
                    .Skip(skip)
                    .Take(limit)
                    .ToListAsync();

                var news = newsQuery.Select(n => new NewsResponseDto
                {
                    NewsId = n.NewsId,
                    Title = n.Title,
                    Content = n.Content,
                    ImageUrl = n.ImageUrl,
                    Tags = n.Tags,
                    CreatedAt = n.CreatedAt,
                    Summary = !string.IsNullOrEmpty(n.Content) && n.Content.Length > 200 ? 
                             n.Content.Substring(0, 200) + "..." : n.Content,
                    TagList = !string.IsNullOrEmpty(n.Tags) ? 
                             n.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                  .Select(t => t.Trim()).ToList() : new List<string>()
                }).ToList();

                var totalNews = await _context.News.CountAsync();

                _logger.LogInformation($"Admin retrieved {news.Count} news items (page {page})");
                return Ok(new
                {
                    news = news,
                    pagination = new
                    {
                        page = page,
                        limit = limit,
                        total = totalNews,
                        totalPages = (int)Math.Ceiling((double)totalNews / limit)
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all news for admin");
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi lấy danh sách tin tức" });
            }
        }

        /// <summary>
        /// Tìm kiếm tin tức (dành cho admin)
        /// </summary>
        [HttpGet("admin/search")]
        [AuthorizeRole(1)] // Chỉ admin
        public async Task<ActionResult<IEnumerable<NewsResponseDto>>> SearchNewsAdmin([FromQuery] string query, [FromQuery] int page = 1, [FromQuery] int limit = 10)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query))
                {
                    return BadRequest(new { message = "Từ khóa tìm kiếm không được để trống" });
                }

                var skip = (page - 1) * limit;
                var searchQuery = query.ToLower().Trim();

                var newsQuery = await _context.News
                    .Where(n => n.Title.ToLower().Contains(searchQuery) ||
                               (n.Content != null && n.Content.ToLower().Contains(searchQuery)) ||
                               (n.Tags != null && n.Tags.ToLower().Contains(searchQuery)))
                    .OrderByDescending(n => n.CreatedAt)
                    .Skip(skip)
                    .Take(limit)
                    .ToListAsync();

                var news = newsQuery.Select(n => new NewsResponseDto
                {
                    NewsId = n.NewsId,
                    Title = n.Title,
                    Content = n.Content,
                    ImageUrl = n.ImageUrl,
                    Tags = n.Tags,
                    CreatedAt = n.CreatedAt,
                    Summary = !string.IsNullOrEmpty(n.Content) && n.Content.Length > 200 ? 
                             n.Content.Substring(0, 200) + "..." : n.Content,
                    TagList = !string.IsNullOrEmpty(n.Tags) ? 
                             n.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                  .Select(t => t.Trim()).ToList() : new List<string>()
                }).ToList();

                var totalResults = await _context.News
                    .Where(n => n.Title.ToLower().Contains(searchQuery) ||
                               (n.Content != null && n.Content.ToLower().Contains(searchQuery)) ||
                               (n.Tags != null && n.Tags.ToLower().Contains(searchQuery)))
                    .CountAsync();

                _logger.LogInformation($"Admin searched news with query '{query}', found {totalResults} results");
                return Ok(new
                {
                    news = news,
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
                _logger.LogError(ex, "Error searching news for admin");
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi tìm kiếm tin tức" });
            }
        }

        /// <summary>
        /// Lấy chi tiết tin tức (dành cho admin)
        /// </summary>
        [HttpGet("admin/{id}")]
        [AuthorizeRole(1)] // Chỉ admin
        public async Task<ActionResult<NewsResponseDto>> GetNewsAdmin(int id)
        {
            try
            {
                var newsItem = await _context.News
                    .Where(n => n.NewsId == id)
                    .FirstOrDefaultAsync();

                if (newsItem == null)
                {
                    return NotFound(new { message = "Không tìm thấy tin tức" });
                }

                var news = new NewsResponseDto
                {
                    NewsId = newsItem.NewsId,
                    Title = newsItem.Title,
                    Content = newsItem.Content,
                    ImageUrl = newsItem.ImageUrl,
                    Tags = newsItem.Tags,
                    CreatedAt = newsItem.CreatedAt,
                    Summary = !string.IsNullOrEmpty(newsItem.Content) && newsItem.Content.Length > 200 ? 
                             newsItem.Content.Substring(0, 200) + "..." : newsItem.Content,
                    TagList = !string.IsNullOrEmpty(newsItem.Tags) ? 
                             newsItem.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                  .Select(t => t.Trim()).ToList() : new List<string>()
                };

                _logger.LogInformation($"Admin retrieved news {id}");
                return Ok(news);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving news {NewsId} for admin", id);
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi lấy thông tin tin tức" });
            }
        }

        /// <summary>
        /// Tạo tin tức mới (dành cho admin)
        /// </summary>
        [HttpPost("admin")]
        [AuthorizeRole(1)] // Chỉ admin
        public async Task<ActionResult<NewsResponseDto>> CreateNewsAdmin([FromBody] CreateNewsDto createDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var news = new News
                {
                    Title = createDto.Title,
                    Content = createDto.Content,
                    ImageUrl = createDto.ImageUrl,
                    Tags = createDto.Tags,
                    CreatedAt = DateTime.UtcNow
                };

                _context.News.Add(news);
                await _context.SaveChangesAsync();

                var response = new NewsResponseDto
                {
                    NewsId = news.NewsId,
                    Title = news.Title,
                    Content = news.Content,
                    ImageUrl = news.ImageUrl,
                    Tags = news.Tags,
                    CreatedAt = news.CreatedAt,
                    Summary = !string.IsNullOrEmpty(news.Content) && news.Content.Length > 200 ? 
                             news.Content.Substring(0, 200) + "..." : news.Content,
                    TagList = !string.IsNullOrEmpty(news.Tags) ? 
                             news.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                  .Select(t => t.Trim()).ToList() : new List<string>()
                };

                _logger.LogInformation($"Admin created news {news.NewsId} - {news.Title}");
                return CreatedAtAction(nameof(GetNewsAdmin), new { id = news.NewsId }, response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating news for admin");
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi tạo tin tức" });
            }
        }

        /// <summary>
        /// Cập nhật tin tức (dành cho admin)
        /// </summary>
        [HttpPut("admin/{id}")]
        [AuthorizeRole(1)] // Chỉ admin
        public async Task<IActionResult> UpdateNewsAdmin(int id, [FromBody] CreateNewsDto updateDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var news = await _context.News.FindAsync(id);
                if (news == null)
                {
                    return NotFound(new { message = "Không tìm thấy tin tức" });
                }

                // Cập nhật thông tin
                news.Title = updateDto.Title;
                news.Content = updateDto.Content;
                news.ImageUrl = updateDto.ImageUrl;
                news.Tags = updateDto.Tags;

                await _context.SaveChangesAsync();

                _logger.LogInformation($"Admin updated news {id}");
                return Ok(new { message = "Cập nhật tin tức thành công" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating news {NewsId} for admin", id);
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi cập nhật tin tức" });
            }
        }

        /// <summary>
        /// Xóa tin tức (dành cho admin)
        /// </summary>
        [HttpDelete("admin/{id}")]
        [AuthorizeRole(1)] // Chỉ admin
        public async Task<IActionResult> DeleteNewsAdmin(int id)
        {
            try
            {
                var news = await _context.News.FindAsync(id);
                if (news == null)
                {
                    return NotFound(new { message = "Không tìm thấy tin tức" });
                }

                _context.News.Remove(news);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Admin deleted news {id} - {news.Title}");
                return Ok(new { message = "Xóa tin tức thành công" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting news {NewsId} for admin", id);
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi xóa tin tức" });
            }
        }

        #endregion
    }
} 