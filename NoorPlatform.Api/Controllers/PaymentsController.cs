using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoorPlatform.Core.Entities;
using NoorPlatform.Infrastructure.Data;
using System.Security.Claims;

namespace NoorPlatform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PaymentsController : ControllerBase
{
    private readonly NoorDbContext _context;

    public PaymentsController(NoorDbContext context)
    {
        _context = context;
    }

    // للمشرف: جلب جميع المدفوعات
    // ─── إصلاح حرج: إضافة ترقيم الصفحات (Pagination) لمنع مشاكل الأداء ───
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAllPayments([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 5, 100);

        var query = _context.Payments.AsQueryable();

        var total = await query.CountAsync();
        var items = await query
            .Include(p => p.Student).ThenInclude(s => s.User)
            .OrderByDescending(p => p.DueDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new
            {
                p.Id,
                StudentName = p.Student.User.FullName,
                p.Amount,
                p.Description,
                p.DueDate,
                p.PaidDate,
                p.Status
            })
            .ToListAsync();

        return Ok(new { total, page, pageSize, items });
    }

    // لولي الأمر: جلب فواتير الأبناء
    [HttpGet("parent")]
    [Authorize(Roles = "Parent")]
    public async Task<IActionResult> GetParentPayments()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var parent = await _context.Parents.FirstOrDefaultAsync(p => p.UserId == userId);
        if (parent == null) return NotFound("Parent not found");

        var payments = await _context.Payments
            .Include(p => p.Student).ThenInclude(s => s.User)
            .Where(p => p.ParentId == parent.Id)
            .Select(p => new
            {
                p.Id,
                StudentName = p.Student.User.FullName,
                p.Amount,
                p.Description,
                p.DueDate,
                p.PaidDate,
                p.Status
            })
            .OrderByDescending(p => p.DueDate)
            .ToListAsync();

        return Ok(payments);
    }

    public class CreatePaymentDto
    {
        public int StudentId { get; set; }
        public decimal Amount { get; set; }
        public string Description { get; set; } = string.Empty;
        public DateTime DueDate { get; set; }
    }

    // للمشرف: إضافة فاتورة جديدة
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreatePayment([FromBody] CreatePaymentDto dto)
    {
        // ─── إصلاح عالي: التحقق من القيمة لضمان عدم تمرير مبالغ سالبة أو صفرية ───
        if (dto.Amount <= 0)
            return BadRequest(new { message = "المبلغ يجب أن يكون أكبر من صفر" });

        var student = await _context.Students.FindAsync(dto.StudentId);
        if (student == null) return NotFound("Student not found");

        if (student.ParentId == null) return BadRequest("Student has no parent assigned");

        var payment = new Payment
        {
            StudentId = student.Id,
            ParentId = student.ParentId.Value,
            Amount = dto.Amount,
            Description = dto.Description,
            DueDate = dto.DueDate,
            Status = PaymentStatus.Pending
        };

        _context.Payments.Add(payment);
        await _context.SaveChangesAsync();
        return Ok(new { 
            payment.Id,
            payment.Amount,
            payment.Description,
            payment.DueDate,
            payment.Status
        });
    }

    // لولي الأمر: دفع الفاتورة (محاكاة)
    [HttpPost("{id}/pay")]
    [Authorize(Roles = "Parent")]
    public async Task<IActionResult> PayInvoice(int id)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var parent = await _context.Parents.FirstOrDefaultAsync(p => p.UserId == userId);
        if (parent == null) return Unauthorized();

        var payment = await _context.Payments.FirstOrDefaultAsync(p => p.Id == id && p.ParentId == parent.Id);
        if (payment == null) return NotFound("Payment not found");

        if (payment.Status == PaymentStatus.Paid)
            return BadRequest("الفاتورة مدفوعة مسبقاً");

        payment.Status = PaymentStatus.Paid;
        payment.PaidDate = DateTime.UtcNow;

        // تسجيل نشاط
        var activity = new ActivityFeed
        {
            UserId = userId,
            UserName = User.FindFirstValue(ClaimTypes.Name) ?? "ولي الأمر",
            ActivityType = "Payment",
            Description = $"تم سداد فاتورة بقيمة {payment.Amount} د.ل للطالب.",
            CreatedAt = DateTime.UtcNow,
            Icon = "💳",
            Color = "text-blue-500"
        };
        _context.ActivityFeeds.Add(activity);

        await _context.SaveChangesAsync();

        // ─── إصلاح عالي: إرجاع DTO محدود يحمي البيانات الحساسة بدلاً من كائن Entity الكامل ───
        var paymentResponse = new
        {
            payment.Id,
            payment.Amount,
            payment.Description,
            payment.DueDate,
            payment.PaidDate,
            payment.Status
        };

        return Ok(new { message = "تم الدفع بنجاح", payment = paymentResponse });
    }

    [HttpPatch("mark-overdue")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> MarkOverduePayments()
    {
        var overduePending = await _context.Payments
            .Where(p => p.Status == PaymentStatus.Pending && p.DueDate < DateTime.UtcNow)
            .ToListAsync();

        if (!overduePending.Any())
            return Ok(new { message = "لا توجد فواتير متأخرة تحتاج للتحديث" });

        foreach (var op in overduePending)
        {
            op.Status = PaymentStatus.Overdue;
        }

        await _context.SaveChangesAsync();
        return Ok(new { message = $"تم تحديث {overduePending.Count} فاتورة كمتأخرة" });
    }
}
