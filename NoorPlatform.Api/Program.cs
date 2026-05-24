using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Server.IIS;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NoorPlatform.Api.Middleware;
using NoorPlatform.Api.Services;
using NoorPlatform.Core.Entities;
using NoorPlatform.Infrastructure.Data;

const long MaxUploadBytes = 52_428_800; // 50 MB

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = MaxUploadBytes;
});

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = MaxUploadBytes;
    options.ValueLengthLimit = int.MaxValue;
    options.MultipartHeadersLengthLimit = int.MaxValue;
});

builder.Services.Configure<IISServerOptions>(options =>
{
    options.MaxRequestBodySize = MaxUploadBytes;
});

builder.Services.AddScoped<AccountProvisioningService>();
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// قاعدة البيانات
builder.Services.AddDbContext<NoorDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
    options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
});

// Identity
builder.Services.AddIdentity<User, IdentityRole<int>>(options =>
{
    options.Password.RequireDigit           = true;
    options.Password.RequiredLength         = 8;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequireUppercase       = true;
    options.Password.RequireLowercase       = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan  = TimeSpan.FromMinutes(15);
})
.AddEntityFrameworkStores<NoorDbContext>()
.AddDefaultTokenProviders();

// JWT — من User Secrets (تطوير) أو متغيرات البيئة Jwt__Key / NOOR_JWT_KEY (إنتاج)
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? Environment.GetEnvironmentVariable("JWT__Key")
    ?? Environment.GetEnvironmentVariable("NOOR_JWT_KEY");
if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey.Length < 32)
    throw new InvalidOperationException(
        "Jwt:Key غير موجود أو قصير. عيّنه عبر: dotnet user-secrets set \"Jwt:Key\" \"<مفتاح-32-حرفاً-على-الأقل>\" أو متغير البيئة Jwt__Key");

var jwtIssuer   = builder.Configuration["Jwt:Issuer"]   ?? "NoorPlatform";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "NoorPlatformClients";
var key         = Encoding.UTF8.GetBytes(jwtKey);

builder.Services.AddAuthentication(x =>
{
    x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    x.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(x =>
{
    x.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
    x.SaveToken = true;
    x.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey         = new SymmetricSecurityKey(key),
        ValidateIssuer           = true,
        ValidIssuer              = jwtIssuer,
        ValidateAudience         = true,
        ValidAudience            = jwtAudience,
        ClockSkew                = TimeSpan.FromMinutes(1)
    };
    x.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            if (context.Exception.GetType() == typeof(SecurityTokenExpiredException))
                context.Response.Headers.Append("Token-Expired", "true");
            return Task.CompletedTask;
        }
    };
});

// Authorization
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly",       policy => policy.RequireRole("Admin"));
    options.AddPolicy("TeacherOrAdmin",  policy => policy.RequireRole("Admin", "Teacher"));
    options.AddPolicy("StudentOrParent", policy => policy.RequireRole("Student", "Parent"));
});

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("Development", b =>
        b.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());

    options.AddPolicy("Production", b =>
        b.WithOrigins(
            "https://noorplatform.com",
            "capacitor://localhost",
            "http://localhost"
        ).AllowAnyMethod().AllowAnyHeader());
});

// ─── HttpClient لخدمة واتساب ───
builder.Services.AddHttpClient("WhatsApp", client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
});

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Noor Platform API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new()
    {
        Description = "أدخل التوكن: Bearer {token}",
        Name        = "Authorization",
        In          = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type        = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme      = "Bearer"
    });
    c.AddSecurityRequirement(new()
    {
        {
            new() { Reference = new() { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// Seed
using (var scope = app.Services.CreateScope())
{
    var services    = scope.ServiceProvider;
    var context     = services.GetRequiredService<NoorDbContext>();
    var userManager = services.GetRequiredService<UserManager<User>>();
    var roleManager = services.GetRequiredService<RoleManager<IdentityRole<int>>>();
    await context.Database.MigrateAsync();
    await DbInitializer.SeedAsync(context, userManager, roleManager, app.Environment.IsProduction());

    // TEMP: Reset password
    var pUser = await userManager.FindByNameAsync("218911437635");
    if (pUser != null)
    {
        var token = await userManager.GeneratePasswordResetTokenAsync(pUser);
        await userManager.ResetPasswordAsync(pUser, token, "Parent123!");
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Noor Platform API v1");
        c.RoutePrefix = "swagger";
    });
}

// ─── Global Exception Handler ───
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json; charset=utf-8";
        var error = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
        var message = app.Environment.IsDevelopment()
            ? error?.Error?.Message ?? "خطأ غير متوقع"
            : "حدث خطأ في الخادم. يرجى المحاولة لاحقاً.";
        var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("GlobalExceptionHandler");
        logger.LogError(error?.Error, "Unhandled exception at {Path}", context.Request.Path);
        await context.Response.WriteAsJsonAsync(new { message, statusCode = 500 });
    });
});

if (!app.Environment.IsDevelopment())
    app.UseHsts();

app.UseHttpsRedirection();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<BlockLibraryUploadsMiddleware>();

var corsPolicy = app.Environment.IsDevelopment() ? "Development" : "Production";
app.UseCors(corsPolicy);

app.UseAuthentication();
app.UseAuthorization();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();

app.Run();
