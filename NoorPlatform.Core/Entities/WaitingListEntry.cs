namespace NoorPlatform.Core.Entities;

public class WaitingListEntry
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    /// <summary>هاتف الطالب — اختياري (كثير من الطلاب الصغار لا يملكون هاتفاً).</summary>
    public string? Phone { get; set; }
    public string ParentName { get; set; } = string.Empty;
    public string ParentPhone { get; set; } = string.Empty;
    public int? Age { get; set; }
    public string RequestedLevel { get; set; } = "مبتدئ";
    public string PreferredTime { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public WaitingListStatus Status { get; set; } = WaitingListStatus.Pending;
    public DateTime RegistrationDate { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
