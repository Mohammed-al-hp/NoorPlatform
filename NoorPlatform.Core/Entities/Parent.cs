namespace NoorPlatform.Core.Entities;

public class Parent
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public string Phone { get; set; } = string.Empty;
    public bool IsDeleted { get; set; } = false;
    public List<Student> Children { get; set; } = new();
}
