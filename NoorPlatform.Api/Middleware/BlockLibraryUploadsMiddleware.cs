using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace NoorPlatform.Api.Middleware;

/// <summary>
/// يمنع الوصول المباشر لملفات المكتبة — يجب التحميل عبر /api/library/{id}/file
/// </summary>
public class BlockLibraryUploadsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<BlockLibraryUploadsMiddleware> _logger;

    public BlockLibraryUploadsMiddleware(RequestDelegate next, ILogger<BlockLibraryUploadsMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/uploads/library", StringComparison.OrdinalIgnoreCase))
        {
            // ─── إصلاح أمني: تسجيل محاولة الوصول غير المصرح بها للـ Audit Trail ───
            var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "Unknown IP";
            _logger.LogWarning("Unauthorized direct file access attempt to library folder from IP {IpAddress}. Path: {Path}", ipAddress, context.Request.Path);

            // ─── إصلاح: إرجاع 403 Forbidden بدلاً من 404 NotFound لتوضيح سبب المنع ───
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json; charset=utf-8";
            await context.Response.WriteAsync("{\"message\": \"الوصول المباشر للملفات غير مصرح به. يرجى تحميل الملف من خلال الواجهة المخصصة في المنصة.\"}");
            return;
        }

        await _next(context);
    }
}
