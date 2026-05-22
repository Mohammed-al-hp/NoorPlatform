namespace NoorPlatform.Core.Entities;

public class Circle
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? TeacherId { get; set; }
    public Teacher? Teacher { get; set; }
    public string Time { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Icon { get; set; } = "✨";
    public List<Student> Students { get; set; } = new();
}
