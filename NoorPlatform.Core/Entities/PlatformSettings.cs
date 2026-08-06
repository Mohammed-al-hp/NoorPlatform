namespace NoorPlatform.Core.Entities;

public class PlatformSettings
{
    public int Id { get; set; }
    public string CenterName { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}