using Microsoft.AspNetCore.Identity;

namespace NoorPlatform.Core.Entities;

public enum UserRole
{
    Admin,
    Teacher,
    Student,
    Parent
}

public class User : IdentityUser<int>
{
    public string FullName { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Student;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
