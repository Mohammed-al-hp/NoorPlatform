namespace NoorPlatform.Api.Middleware;

/// <summary>
/// يفرض تغيير كلمة المرور المؤقتة على مستوى الخادم: الحساب الذي يجب أن يغيّر كلمة مروره
/// لا يصل إلا إلى مسارات /api/auth (مثل change-password) حتى يغيّرها.
/// القيمة تُقرأ من قاعدة البيانات عند التحقق من التوكن (OnTokenValidated) فتعكس الحالة الحالية.
/// </summary>
public class MustChangePasswordGuard
{
    public const string ItemKey = "Noor.MustChangePassword";
    public const string ErrorCode = "MUST_CHANGE_PASSWORD";

    private readonly RequestDelegate _next;

    public MustChangePasswordGuard(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path;
        if (context.Items.TryGetValue(ItemKey, out var flag) && flag is true
            && path.StartsWithSegments("/api")
            && !path.StartsWithSegments("/api/auth")
            && !path.StartsWithSegments("/api/health"))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json; charset=utf-8";
            await context.Response.WriteAsJsonAsync(new
            {
                code = ErrorCode,
                message = "يجب تغيير كلمة المرور المؤقتة قبل متابعة استخدام المنصة"
            });
            return;
        }

        await _next(context);
    }
}
