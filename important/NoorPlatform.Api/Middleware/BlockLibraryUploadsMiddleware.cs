namespace NoorPlatform.Api.Middleware;

/// <summary>
/// يمنع الوصول المباشر لملفات المكتبة — يجب التحميل عبر /api/library/{id}/file
/// </summary>
public class BlockLibraryUploadsMiddleware
{
    private readonly RequestDelegate _next;

    public BlockLibraryUploadsMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/uploads/library", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        await _next(context);
    }
}
