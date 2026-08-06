namespace NoorPlatform.Core.Entities;

public class Teacher
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public string Qualification { get; set; } = string.Empty;
    public DateOnly? BirthDate { get; set; }
    public double AverageRating { get; set; } = 0.0;
    public bool IsDeleted { get; set; } = false;
    public List<Circle> Circles { get; set; } = new();
}