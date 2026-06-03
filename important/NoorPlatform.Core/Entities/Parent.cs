namespace NoorPlatform.Core.Entities;

public class Parent
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public string Phone { get; set; } = string.Empty;
    public List<Student> Children { get; set; } = new();
}
