using System;

namespace NoorPlatform.Core.Entities;

public class Payment
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;

    public int ParentId { get; set; }
    public Parent Parent { get; set; } = null!;

    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime DueDate { get; set; }
    public DateTime? PaidDate { get; set; }
    
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
}
