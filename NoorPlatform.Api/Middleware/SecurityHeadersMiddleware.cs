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

        // SAMEORIGIN يسمح بعارض PDF داخل iframe (blob) على نفس الموقع

        headers["X-Frame-Options"] = "SAMEORIGIN";

        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        headers["X-XSS-Protection"] = "0";
        headers["Cross-Origin-Opener-Policy"] = "same-origin";
        headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";



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



        // default-src: fallback for directives not listed below

        // script-src: inline handlers in index.html + html2pdf from cdnjs

        // style-src / font-src: Google Fonts (link + @font-face files)

        // connect-src: fetch/XHR — API ('self'), SW cache fetches, font/CDN subresources

        // worker-src / manifest-src: PWA service worker + manifest.json

        // frame-src / img-src blob:: PDF viewer + html2pdf output

        // form-action: login/register and upload forms post to same origin (/api)

        return string.Join("; ",

            "default-src 'self'",

            $"script-src 'self' 'unsafe-inline' {trusted}",

            $"style-src 'self' 'unsafe-inline' {trusted}",

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


