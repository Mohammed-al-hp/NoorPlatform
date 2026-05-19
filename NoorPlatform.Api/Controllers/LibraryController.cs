using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoorPlatform.Core.Entities;
using NoorPlatform.Infrastructure.Data;
using System.Security.Claims;

namespace NoorPlatform.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LibraryController : ControllerBase
    {
        private readonly NoorDbContext _context;
        private readonly IWebHostEnvironment _env;

        public LibraryController(NoorDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // GET /api/library
        [HttpGet]
        [Authorize(Roles = "Admin,Teacher,Student")]
        public async Task<IActionResult> GetAll([FromQuery] string? category, [FromQuery] string? search)
        {
            var query = _context.LibraryItems
                .Include(l => l.UploadedByUser)
                .AsQueryable();

            if (!string.IsNullOrEmpty(category))
                query = query.Where(l => l.Category == category);

            if (!string.IsNullOrEmpty(search))
                query = query.Where(l => l.Title.Contains(search) || l.Description.Contains(search));

            var items = await query.OrderByDescending(l => l.CreatedAt).ToListAsync();

            var result = items.Select(l => new
            {
                l.Id,
                l.Title,
                l.Description,
                l.Category,
                l.PdfFilePath,
                UploadedBy = l.UploadedByUser?.FullName ?? "غير معروف",
                l.DownloadCount,
                l.CreatedAt
            });

            return Ok(result);
        }

        // POST /api/library/upload
        [HttpPost("upload")]
        [Authorize(Roles = "Admin,Teacher")]
        public async Task<IActionResult> Upload([FromForm] UploadLibraryRequest request)
        {
            if (request.File == null || request.File.Length == 0)
                return BadRequest(new { message = "الرجاء اختيار ملف" });

            if (Path.GetExtension(request.File.FileName).ToLower() != ".pdf")
                return BadRequest(new { message = "يسمح فقط برفع ملفات PDF" });

            if (request.File.Length > 20 * 1024 * 1024) // 20 MB Limit
                return BadRequest(new { message = "حجم الملف يتجاوز الحد المسموح (20MB)" });

            // Ensure directory exists
            var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "library");
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            // Generate unique filename
            var uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(request.File.FileName);
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await request.File.CopyToAsync(stream);
            }

            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var item = new LibraryItem
            {
                Title = request.Title,
                Description = request.Description ?? "",
                Category = request.Category,
                PdfFilePath = $"/uploads/library/{uniqueFileName}",
                UploadedByUserId = userId,
                CircleId = request.CircleId
            };

            _context.LibraryItems.Add(item);

            // Activity Feed
            _context.ActivityFeeds.Add(new ActivityFeed
            {
                UserId = userId,
                UserName = User.Identity?.Name ?? "User",
                ActivityType = "Library",
                Description = $"تم رفع ملف جديد إلى المكتبة: {item.Title}",
                Icon = "📚",
                Color = "purple"
            });

            await _context.SaveChangesAsync();

            return Ok(new { message = "تم رفع الملف بنجاح", item.Id });
        }

        // DELETE /api/library/{id}
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin,Teacher")]
        public async Task<IActionResult> Delete(int id)
        {
            var item = await _context.LibraryItems.FindAsync(id);
            if (item == null) return NotFound(new { message = "الملف غير موجود" });

            // Delete physical file
            var physicalPath = Path.Combine(_env.WebRootPath, item.PdfFilePath.TrimStart('/'));
            if (System.IO.File.Exists(physicalPath))
            {
                System.IO.File.Delete(physicalPath);
            }

            _context.LibraryItems.Remove(item);
            await _context.SaveChangesAsync();

            return Ok(new { message = "تم حذف الملف بنجاح" });
        }

        // POST /api/library/{id}/download
        [HttpPost("{id}/download")]
        [Authorize(Roles = "Admin,Teacher,Student")]
        public async Task<IActionResult> RecordDownload(int id)
        {
            var item = await _context.LibraryItems.FindAsync(id);
            if (item == null) return NotFound();

            item.DownloadCount++;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Download recorded" });
        }
    }

    public class UploadLibraryRequest
    {
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Category { get; set; } = string.Empty;
        public int? CircleId { get; set; }
        public IFormFile File { get; set; } = null!;
    }
}
