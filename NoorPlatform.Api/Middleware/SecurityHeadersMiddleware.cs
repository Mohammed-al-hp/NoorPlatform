namespace NoorPlatform.Api.Middleware;

public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IWebHostEnvironment _env;

    /// <summary>
    /// Trusted third-party origins used by wwwroot/index.html and sw.js.
    /// </summary>
    private static readonly string[] TrustedFontAndCdnOrigins =
    [
        "https://fonts.googleapis.com",
        "https://fonts.gstatic.com",
        "https://cdnjs.cloudflare.com"
    ];

    public SecurityHeadersMiddleware(RequestDelegate next, IWebHostEnvironment env)
    {
        _next = next;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;

        headers["X-Content-Type-Options"] = "nosniff";
        
        // ─── إصلاح أمني: تم إزالة X-Frame-Options والاعتماد كلياً على frame-ancestors الأحدث في CSP ───
        
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        headers["X-XSS-Protection"] = "0";
        headers["Cross-Origin-Opener-Policy"] = "same-origin";
        headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";

        // ─── إصلاح أمني: إضافة ترويسة HSTS بشكل دائم لتطبيق HTTPS بصرامة ───
        headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";

        if (!context.Request.Path.StartsWithSegments("/swagger"))
            headers["Content-Security-Policy"] = BuildContentSecurityPolicy();

        await _next(context);
    }

    private string BuildContentSecurityPolicy()
    {
        var trusted = string.Join(' ', TrustedFontAndCdnOrigins);
        var connectSrc = $"'self' {trusted}";

        if (_env.IsDevelopment())
        {
            // ASP.NET Core browser refresh (dotnet watch) + local API over WebSocket
            connectSrc += " ws://localhost:* wss://localhost:* http://localhost:* https://localhost:*";
        }

        // ─── إصلاح أمني: تم إعادة 'unsafe-inline' لأن الواجهة الأمامية تعتمد بكثافة على (onclick) و (inline scripts) في 100+ موضع. ───
        return string.Join("; ",
            "default-src 'self'",
            $"script-src 'self' 'unsafe-inline' {trusted}",
            $"style-src 'self' 'unsafe-inline' {trusted}", // style-src 'unsafe-inline' عادة مقبول لبعض الإطارات ولكن الأهم حماية الـ script-src
            $"font-src 'self' data: {trusted}",
            $"connect-src {connectSrc}",
            "img-src 'self' data: blob:",
            "frame-src 'self' blob:",
            "worker-src 'self'",
            "manifest-src 'self'",
            "object-src 'none'",
            "base-uri 'self'",
            "form-action 'self'",
            "frame-ancestors 'none'");
    }
}
