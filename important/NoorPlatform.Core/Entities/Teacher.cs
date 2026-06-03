namespace NoorPlatform.Core.Entities;

public class Teacher
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public string Qualification { get; set; } = string.Empty;
    public List<Circle> Circles { get; set; } = new();
}
