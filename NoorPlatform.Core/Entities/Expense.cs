using System;

namespace NoorPlatform.Core.Entities;

public enum ExpenseCategory
{
    Salary,       // راتب محفظ أو موظف
    Trust,        // عهدة مالية
    Maintenance,  // صيانة ونثريات
    Reward,       // مكافآت للطلبة / جوائز مسابقات
    Other         // أخرى
}

public class Expense
{
    public int Id { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime Date { get; set; } = DateTime.UtcNow;
    
    public ExpenseCategory Category { get; set; }

    public int? RecordedByUserId { get; set; }
    public User? RecordedByUser { get; set; }
}
