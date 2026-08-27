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
public class ExpensesController : ControllerBase
{
    private readonly NoorDbContext _context;

    public ExpensesController(NoorDbContext context)
    {
        _context = context;
    }

    // ─── جلب جميع المصروفات مع ترقيم الصفحات ───
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? category = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 5, 100);

        var query = _context.Expenses.AsQueryable();

        if (!string.IsNullOrEmpty(category) && Enum.TryParse<ExpenseCategory>(category, true, out var cat))
            query = query.Where(e => e.Category == cat);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(e => e.Date)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new
            {
                e.Id,
                e.Amount,
                e.Description,
                e.Date,
                Category = e.Category.ToString(),
                RecordedBy = e.RecordedByUser != null ? e.RecordedByUser.FullName : "—"
            })
            .ToListAsync();

        // إجمالي المصروفات (كل الصفحات)
        var totalAmount = await query.SumAsync(e => e.Amount);

        return Ok(new { total, totalAmount, page, pageSize, items });
    }

    // ─── جلب ملخص المصروفات حسب الفئة ───
    [HttpGet("summary")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetSummary(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        var query = _context.Expenses.AsQueryable();

        if (from.HasValue) query = query.Where(e => e.Date >= from.Value);
        if (to.HasValue) query = query.Where(e => e.Date <= to.Value);

        var summary = await query
            .GroupBy(e => e.Category)
            .Select(g => new
            {
                Category = g.Key.ToString(),
                Total = g.Sum(e => e.Amount),
                Count = g.Count()
            })
            .ToListAsync();

        var grandTotal = summary.Sum(s => s.Total);

        return Ok(new { grandTotal, breakdown = summary });
    }

    public class CreateExpenseDto
    {
        public decimal Amount { get; set; }
        public string Description { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public string Category { get; set; } = "Other";
    }

    // ─── إنشاء مصروف جديد ───
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] CreateExpenseDto dto)
    {
        if (dto.Amount <= 0)
            return BadRequest(new { message = "المبلغ يجب أن يكون أكبر من صفر" });

        if (!Enum.TryParse<ExpenseCategory>(dto.Category, true, out var category))
            return BadRequest(new { message = "فئة المصروف غير صحيحة. الفئات المتاحة: Salary, Trust, Maintenance, Reward, Other" });

        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var expense = new Expense
        {
            Amount = dto.Amount,
            Description = dto.Description,
            Date = dto.Date,
            Category = category,
            RecordedByUserId = userId
        };

        _context.Expenses.Add(expense);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = expense.Id }, new
        {
            expense.Id,
            expense.Amount,
            expense.Description,
            expense.Date,
            Category = expense.Category.ToString()
        });
    }

    // ─── جلب مصروف بالمعرف ───
    [HttpGet("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetById(int id)
    {
        var expense = await _context.Expenses
            .Include(e => e.RecordedByUser)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (expense == null)
            return NotFound(new { message = "المصروف غير موجود" });

        return Ok(new
        {
            expense.Id,
            expense.Amount,
            expense.Description,
            expense.Date,
            Category = expense.Category.ToString(),
            RecordedBy = expense.RecordedByUser?.FullName ?? "—"
        });
    }

    // ─── تعديل مصروف ───
    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, [FromBody] CreateExpenseDto dto)
    {
        var expense = await _context.Expenses.FindAsync(id);
        if (expense == null)
            return NotFound(new { message = "المصروف غير موجود" });

        if (dto.Amount <= 0)
            return BadRequest(new { message = "المبلغ يجب أن يكون أكبر من صفر" });

        if (!Enum.TryParse<ExpenseCategory>(dto.Category, true, out var category))
            return BadRequest(new { message = "فئة المصروف غير صحيحة" });

        expense.Amount = dto.Amount;
        expense.Description = dto.Description;
        expense.Date = dto.Date;
        expense.Category = category;

        await _context.SaveChangesAsync();
        return Ok(new { message = "تم تحديث المصروف بنجاح" });
    }

    // ─── حذف مصروف ───
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var expense = await _context.Expenses.FindAsync(id);
        if (expense == null)
            return NotFound(new { message = "المصروف غير موجود" });

        _context.Expenses.Remove(expense);
        await _context.SaveChangesAsync();
        return Ok(new { message = "تم حذف المصروف بنجاح" });
    }
}
