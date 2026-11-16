using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using NoorPlatform.Core.Entities;
using NoorPlatform.Infrastructure.Data;

namespace NoorPlatform.Api.Security;

/// <summary>
/// يقيّد الإجراء للمشرف الذي يمتلك صلاحية محددة (أو المدير العام).
/// لا يؤثر على غير المشرفين: أدوارهم تُضبط بـ [Authorize(Roles=...)] كالمعتاد.
/// تُقرأ الصلاحيات من قاعدة البيانات في كل طلب حتى ينعكس تعديلها فوراً دون إعادة الدخول.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class RequireAdminPermissionAttribute : Attribute, IAsyncAuthorizationFilter
{
    private readonly AdminPermission _permission;

    public RequireAdminPermissionAttribute(AdminPermission permission) => _permission = permission;

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;
        if (user.Identity?.IsAuthenticated != true || !user.IsInRole("Admin"))
            return;

        var userId = AuthorizationHelpers.GetUserId(user);
        if (userId == null) return;

        var db = context.HttpContext.RequestServices.GetRequiredService<NoorDbContext>();
        var allowed = await AdminAccess.HasPermissionAsync(db, userId.Value, _permission);
        if (!allowed)
            context.Result = new ObjectResult(new { message = AdminAccess.DeniedMessage(_permission) })
            { StatusCode = StatusCodes.Status403Forbidden };
    }
}

/// <summary>يقيّد الإجراء للمدير العام فقط.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequireSuperAdminAttribute : Attribute, IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var userId = AuthorizationHelpers.GetUserId(context.HttpContext.User);
        var db = context.HttpContext.RequestServices.GetRequiredService<NoorDbContext>();
        if (userId == null || !await AdminAccess.IsSuperAdminAsync(db, userId.Value))
            context.Result = new ObjectResult(new { message = "هذا الإجراء متاح للمدير العام فقط" })
            { StatusCode = StatusCodes.Status403Forbidden };
    }
}

public static class AdminAccess
{
    public static Task<bool> IsSuperAdminAsync(NoorDbContext db, int userId) =>
        db.Users.AsNoTracking().AnyAsync(u => u.Id == userId && u.Role == UserRole.Admin && u.IsSuperAdmin);

    public static async Task<bool> HasPermissionAsync(NoorDbContext db, int userId, AdminPermission permission)
    {
        var u = await db.Users.AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(x => new { x.Role, x.IsSuperAdmin, x.AdminPermissions })
            .FirstOrDefaultAsync();
        return u != null && u.Role == UserRole.Admin &&
               (u.IsSuperAdmin || (u.AdminPermissions & permission) == permission);
    }

    public static string DeniedMessage(AdminPermission permission) => permission switch
    {
        AdminPermission.ManageFinance => "لا تملك صلاحية إدارة الشؤون المالية. تواصل مع المدير العام.",
        AdminPermission.DeleteRecords => "لا تملك صلاحية الحذف. تواصل مع المدير العام.",
        AdminPermission.ManageSettings => "لا تملك صلاحية تعديل إعدادات النظام. تواصل مع المدير العام.",
        _ => "لا تملك الصلاحية لهذا الإجراء."
    };

    /// <summary>قائمة الصلاحيات بصيغة نصية للواجهات.</summary>
    public static string[] ToNames(bool isSuperAdmin, AdminPermission permissions)
    {
        var effective = isSuperAdmin ? AdminPermission.All : permissions;
        var names = new List<string>();
        if ((effective & AdminPermission.ManageFinance) != 0) names.Add("ManageFinance");
        if ((effective & AdminPermission.DeleteRecords) != 0) names.Add("DeleteRecords");
        if ((effective & AdminPermission.ManageSettings) != 0) names.Add("ManageSettings");
        if (isSuperAdmin) names.Add("ManageAdmins");
        return names.ToArray();
    }
}
