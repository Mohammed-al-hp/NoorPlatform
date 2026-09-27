namespace NoorPlatform.Core.Entities;

/// <summary>متن معرَّف بالمنصة (مثل تحفة الأطفال، الجزرية) يُدار من الإعدادات.</summary>
public class Matn
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}