using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoorPlatform.Api.Security;
using NoorPlatform.Core.Entities;
using NoorPlatform.Infrastructure.Data;
using System.Security.Claims;

namespace NoorPlatform.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
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
        [Authorize(Roles = "Admin,Teacher,Student,Parent")]
        public async Task<IActionResult> GetAll([FromQuery] string? category, [FromQuery] string? search)
        {
            var query = _context.LibraryItems.AsQueryable();

            if (!string.IsNullOrEmpty(category))
                query = query.Where(l => l.Category == category);

            if (!string.IsNullOrEmpty(search))
                query = query.Where(l => l.Title.Contains(search) || l.Description.Contains(search));

            var result = await query
                .OrderByDescending(l => l.CreatedAt)
                .Select(l => new
                {
                    l.Id,
                    l.Title,
                    l.Description,
                    l.Category,
                    l.PdfFilePath,
                    UploadedBy = l.UploadedByUser != null ? l.UploadedByUser.FullName : "غير معروف",
                    l.DownloadCount,
                    l.CreatedAt
                })
                .ToListAsync();

            return Ok(result);
        }

        // POST /api/library/upload
        [HttpPost("upload")]
        [Authorize(Roles = "Admin,Teacher")]
        [RequestSizeLimit(52_428_800)]
        [RequestFormLimits(MultipartBodyLengthLimit = 52_428_800)]
        public async Task<IActionResult> Upload([FromForm] UploadLibraryRequest request)
        {
            if (request.File == null || request.File.Length == 0)
                return BadRequest(new { message = "الرجاء اختيار ملف" });

            if (Path.GetExtension(request.File.FileName).ToLowerInvariant() != ".pdf")
                return BadRequest(new { message = "يسمح فقط برفع ملفات PDF" });

            if (request.File.Length > 50 * 1024 * 1024)
                return BadRequest(new { message = "حجم الملف يتجاوز الحد المسموح (50MB)" });

            var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "library");
            Directory.CreateDirectory(uploadsFolder);

            var safeName = SafePathHelper.SanitizeUploadFileName(request.File.FileName);
            var uniqueFileName = $"{Guid.NewGuid():N}_{safeName}";
            if (!SafePathHelper.TryResolveUnderWebRoot(_env.WebRootPath, Path.Combine("uploads", "library", uniqueFileName), out var filePath))
                return BadRequest(new { message = "مسار الملف غير صالح" });

            await using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await request.File.CopyToAsync(stream);
            }

            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var item = new LibraryItem
            {
                Title = request.Title.Trim(),
                Description = request.Description?.Trim() ?? "",
                Category = request.Category,
                PdfFilePath = $"/uploads/library/{uniqueFileName}",
                UploadedByUserId = userId,
                CircleId = request.CircleId
            };

            _context.LibraryItems.Add(item);

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

            if (SafePathHelper.TryResolveUnderWebRoot(_env.WebRootPath, item.PdfFilePath.TrimStart('/'), out var physicalPath)
                && System.IO.File.Exists(physicalPath))
            {
                System.IO.File.Delete(physicalPath);
            }

            _context.LibraryItems.Remove(item);
            await _context.SaveChangesAsync();

            return Ok(new { message = "تم حذف الملف بنجاح" });
        }

        // GET /api/library/{id}/file — تحميل PDF مع التحقق من الصلاحية
        [HttpGet("{id}/file")]
        [Authorize(Roles = "Admin,Teacher,Student,Parent")]
        public async Task<IActionResult> GetFile(int id)
        {
            var item = await _context.LibraryItems.FindAsync(id);
            if (item == null)
                return NotFound(new { message = "الملف غير موجود" });

            if (!SafePathHelper.TryResolveUnderWebRoot(_env.WebRootPath, item.PdfFilePath.TrimStart('/'), out var physicalPath)
                || !System.IO.File.Exists(physicalPath))
                return NotFound(new { message = "الملف غير موجود على الخادم" });

            item.DownloadCount++;
            await _context.SaveChangesAsync();

            return PhysicalFile(physicalPath, "application/pdf", $"{item.Title}.pdf");
        }

        // POST /api/library/{id}/download — للتوافق مع العملاء القديمة (لا يُضاعف العداد إن استُخدم /file)
        [HttpPost("{id}/download")]
        [Authorize(Roles = "Admin,Teacher,Student,Parent")]
        public async Task<IActionResult> RecordDownload(int id)
        {
            var item = await _context.LibraryItems.FindAsync(id);
            if (item == null) return NotFound();

            item.DownloadCount++;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Download recorded", downloadCount = item.DownloadCount });
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
