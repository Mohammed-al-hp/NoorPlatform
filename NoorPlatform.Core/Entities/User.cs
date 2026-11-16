using Microsoft.AspNetCore.Identity;

namespace NoorPlatform.Core.Entities;

public enum UserRole
{
    Admin,
    Teacher,
    Student,
    Parent
}

/// <summary>
/// صلاحيات المشرف القابلة للتحكم. «المدير العام» (IsSuperAdmin) يمتلكها جميعاً دائماً،
/// أما المشرف العادي فيُمنح منها ما يحدده المدير العام.
/// </summary>
[Flags]
public enum AdminPermission
{
    None = 0,
    /// <summary>الرسوم والمدفوعات والمصروفات.</summary>
    ManageFinance = 1,
    /// <summary>حذف/أرشفة الطلاب والمحفّظين والحلقات والمستخدمين.</summary>
    DeleteRecords = 2,
    /// <summary>تعديل إعدادات النظام (اسم المركز، النقاط، أوزان التقييم، قناة التحقق...).</summary>
    ManageSettings = 4,

    /// <summary>الصلاحيات الافتراضية لمشرف جديد.</summary>
    Default = ManageFinance | DeleteRecords,
    All = ManageFinance | DeleteRecords | ManageSettings
}

public class User : IdentityUser<int>
{
    public string FullName { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Student;
    public bool MustChangePassword { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }

    /// <summary>المدير العام: كل الصلاحيات بما فيها إدارة المشرفين. ذو معنى لدور Admin فقط.</summary>
    public bool IsSuperAdmin { get; set; }

    /// <summary>صلاحيات المشرف العادي (تُتجاهل للمدير العام).</summary>
    public AdminPermission AdminPermissions { get; set; } = AdminPermission.Default;

    public bool HasAdminPermission(AdminPermission permission) =>
        Role == UserRole.Admin && (IsSuperAdmin || (AdminPermissions & permission) == permission);
}
