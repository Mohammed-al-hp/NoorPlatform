using NoorPlatform.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoorPlatform.Core.Entities;
using NoorPlatform.Infrastructure.Data;
using System.Linq;
using System.Security.Claims;

namespace NoorPlatform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[RequireAdminPermission(AdminPermission.ManageFinance)]
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

        var query = _context.Payments.IgnoreQueryFilters().AsQueryable();

        var total = await query.CountAsync();
        var summaryRows = await query
            .GroupBy(p => p.Status)
            .Select(g => new { Status = g.Key, Count = g.Count(), Amount = g.Sum(x => x.Amount) })
            .ToListAsync();

        decimal SumStatus(PaymentStatus status) =>
            summaryRows.FirstOrDefault(r => r.Status == status)?.Amount ?? 0;
        int CountStatus(PaymentStatus status) =>
            summaryRows.FirstOrDefault(r => r.Status == status)?.Count ?? 0;

        var summary = new
        {
            paidAmount = SumStatus(PaymentStatus.Paid),
            paidCount = CountStatus(PaymentStatus.Paid),
            pendingAmount = SumStatus(PaymentStatus.Pending),
            pendingCount = CountStatus(PaymentStatus.Pending),
            overdueAmount = SumStatus(PaymentStatus.Overdue),
            overdueCount = CountStatus(PaymentStatus.Overdue),
            cancelledCount = CountStatus(PaymentStatus.Cancelled),
            // الفواتير الملغاة لا تدخل في إجمالي المستحقات
            totalAmount = summaryRows.Where(r => r.Status != PaymentStatus.Cancelled).Sum(r => r.Amount),
            totalCount = total - CountStatus(PaymentStatus.Cancelled)
        };

        var items = await query
            .IgnoreQueryFilters()
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

        return Ok(new { total, page, pageSize, items, summary });
    }

    // لولي الأمر: جلب فواتير الأبناء
    [HttpGet("parent")]
    [Authorize(Roles = "Parent")]
    public async Task<IActionResult> GetParentPayments()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var parent = await _context.Parents.FirstOrDefaultAsync(p => p.UserId == userId);
        if (parent == null) return NotFound(new { message = "لم يُعثر على بيانات ولي الأمر" });

        var payments = await _context.Payments
            .IgnoreQueryFilters()
            .Include(p => p.Student).ThenInclude(s => s.User)
            .Where(p => p.ParentId == parent.Id && p.Status != PaymentStatus.Cancelled)
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
        var validation = ValidatePayment(dto.Amount, dto.Description, dto.DueDate);
        if (validation != null)
            return BadRequest(new { message = validation });

        var student = await _context.Students.FindAsync(dto.StudentId);
        if (student == null) return NotFound(new { message = "الطالب غير موجود" });

        if (student.ParentId == null) return BadRequest(new { message = "الطالب غير مرتبط بولي أمر" });

        var payment = new Payment
        {
            StudentId = student.Id,
            ParentId = student.ParentId.Value,
            Amount = dto.Amount,
            Description = dto.Description.Trim(),
            DueDate = dto.DueDate.Date,
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

    // للمشرف: تأكيد استلام المبلغ نقداً
    [HttpPost("{id}/pay")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> PayInvoice(int id)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var payment = await _context.Payments.FirstOrDefaultAsync(p => p.Id == id);
        if (payment == null) return NotFound(new { message = "الفاتورة غير موجودة" });

        if (payment.Status == PaymentStatus.Paid)
            return BadRequest(new { message = "الفاتورة مدفوعة مسبقاً" });
        if (payment.Status == PaymentStatus.Cancelled)
            return BadRequest(new { message = "لا يمكن تحصيل فاتورة ملغاة" });

        payment.Status = PaymentStatus.Paid;
        payment.PaidDate = DateTime.UtcNow;

        // تسجيل نشاط
        var activity = new ActivityFeed
        {
            UserId = userId,
            UserName = User.FindFirstValue(ClaimTypes.Name) ?? "المشرف",
            ActivityType = "Payment",
            Description = $"تم سداد فاتورة  بقيمة {payment.Amount} د.ل (استلام نقدي).",
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

    public class UpdatePaymentDto
    {
        public decimal Amount { get; set; }
        public string Description { get; set; } = string.Empty;
        public DateTime DueDate { get; set; }
    }

    // للمشرف: تعديل فاتورة غير مدفوعة (تصحيح المبلغ أو الوصف أو تاريخ الاستحقاق)
    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdatePayment(int id, [FromBody] UpdatePaymentDto dto)
    {
        var payment = await _context.Payments.FirstOrDefaultAsync(p => p.Id == id);
        if (payment == null) return NotFound(new { message = "الفاتورة غير موجودة" });
        if (payment.Status is PaymentStatus.Paid or PaymentStatus.Cancelled)
            return BadRequest(new { message = "لا يمكن تعديل فاتورة مدفوعة أو ملغاة" });

        var validation = ValidatePayment(dto.Amount, dto.Description, dto.DueDate);
        if (validation != null)
            return BadRequest(new { message = validation });

        payment.Amount = dto.Amount;
        payment.Description = dto.Description.Trim();
        payment.DueDate = dto.DueDate.Date;
        // إعادة تقييم التأخير بعد تغيير تاريخ الاستحقاق
        payment.Status = payment.DueDate < DateTime.UtcNow.Date ? PaymentStatus.Overdue : PaymentStatus.Pending;

        await _context.SaveChangesAsync();
        return Ok(new { message = "تم تعديل الفاتورة", payment.Id, payment.Status });
    }

    // للمشرف: إلغاء فاتورة صدرت بالخطأ (تبقى في السجل للتدقيق ولا تظهر لولي الأمر)
    [HttpPost("{id}/cancel")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CancelPayment(int id)
    {
        var payment = await _context.Payments.FirstOrDefaultAsync(p => p.Id == id);
        if (payment == null) return NotFound(new { message = "الفاتورة غير موجودة" });
        if (payment.Status == PaymentStatus.Paid)
            return BadRequest(new { message = "لا يمكن إلغاء فاتورة مدفوعة" });
        if (payment.Status == PaymentStatus.Cancelled)
            return BadRequest(new { message = "الفاتورة ملغاة مسبقاً" });

        payment.Status = PaymentStatus.Cancelled;
        _context.ActivityFeeds.Add(new ActivityFeed
        {
            UserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!),
            UserName = User.FindFirstValue(ClaimTypes.Name) ?? "المشرف",
            ActivityType = "Payment",
            Description = $"تم إلغاء فاتورة بقيمة {payment.Amount} د.ل",
            Icon = "🧾",
            Color = "text-gray-500"
        });
        await _context.SaveChangesAsync();
        return Ok(new { message = "تم إلغاء الفاتورة" });
    }

    private static string? ValidatePayment(decimal amount, string? description, DateTime dueDate)
    {
        if (amount <= 0)
            return "المبلغ يجب أن يكون أكبر من صفر";
        if (amount > 1_000_000)
            return "المبلغ كبير بشكل غير منطقي";
        if (string.IsNullOrWhiteSpace(description))
            return "وصف الفاتورة مطلوب";
        if (description.Trim().Length > 200)
            return "وصف الفاتورة طويل جداً (الحد 200 حرف)";
        if (dueDate.Year < 2020 || dueDate > DateTime.UtcNow.AddYears(2))
            return "تاريخ الاستحقاق غير صالح";
        return null;
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
