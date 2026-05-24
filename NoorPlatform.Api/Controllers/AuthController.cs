using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using NoorPlatform.Api.Services;
using NoorPlatform.Core.Entities;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using NoorPlatform.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace NoorPlatform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<User> _userManager;
    private readonly SignInManager<User> _signInManager;
    private readonly IConfiguration _configuration;
    private readonly NoorDbContext _context;

    public AuthController(UserManager<User> userManager, SignInManager<User> signInManager, IConfiguration configuration, NoorDbContext context)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _configuration = configuration;
        _context = context;
    }

    // POST /api/auth/login — رقم الهاتف + كلمة المرور
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { message = "بيانات تسجيل الدخول غير صالحة" });

        var lookupKeys = LibyanPhone.GetLoginLookupKeys(request.Phone);
        var candidates = new List<User>();
        foreach (var key in lookupKeys)
        {
            var match = await _userManager.FindByNameAsync(key)
                        ?? await _userManager.Users.FirstOrDefaultAsync(u =>
                            u.PhoneNumber == key || u.NormalizedUserName == key.ToUpperInvariant());
            if (match != null && candidates.All(c => c.Id != match.Id))
                candidates.Add(match);
        }

        User? user = null;
        foreach (var candidate in candidates)
        {
            if (!candidate.IsActive) continue;
            var check = await _signInManager.CheckPasswordSignInAsync(candidate, request.Password, lockoutOnFailure: false);
            if (check.Succeeded)
            {
                user = candidate;
                break;
            }
        }

        if (user == null)
        {
            if (candidates.Count > 0)
                await _signInManager.CheckPasswordSignInAsync(candidates[0], request.Password, lockoutOnFailure: true);
            return Unauthorized(new { message = "رقم الهاتف أو كلمة المرور غير صحيحة" });
        }

        user.LastLoginAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        if (user.Role == UserRole.Student)
        {
            var isDeleted = await _context.Students.IgnoreQueryFilters().AnyAsync(s => s.UserId == user.Id && s.IsDeleted);
            if (isDeleted)
                return Unauthorized(new { message = "هذا الحساب مؤرشف ولا يمكن تسجيل الدخول إليه." });
        }

        var token = GenerateJwtToken(user);
        return Ok(new
        {
            token,
            mustChangePassword = user.MustChangePassword,
            user = new
            {
                user.Id,
                user.FullName,
                phone = AccountProvisioningService.ToDisplayPhone(user.UserName ?? user.PhoneNumber ?? request.Phone),
                user.Email,
                role = user.Role.ToString()
            }
        });
    }

    // POST /api/auth/change-password — إجباري عند أول دخول
    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { message = "بيانات غير صالحة" });

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = await _userManager.FindByIdAsync(userId!);
        if (user == null)
            return NotFound();

        var check = await _userManager.CheckPasswordAsync(user, request.CurrentPassword);
        if (!check)
            return BadRequest(new { message = "كلمة المرور الحالية غير صحيحة" });

        var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
            return BadRequest(new { message = string.Join("، ", result.Errors.Select(e => e.Description)) });

        user.MustChangePassword = false;
        await _userManager.UpdateAsync(user);

        return Ok(new { message = "تم تغيير كلمة المرور بنجاح" });
    }

    // التسجيل الذاتي معطّل — الحسابات تُنشأ من الإدارة فقط
    [HttpPost("register")]
    public IActionResult Register()
    {
        return StatusCode(403, new { message = "التسجيل الذاتي غير متاح. يرجى التواصل مع إدارة المركز." });
    }

    private string GenerateJwtToken(User user)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var keyStr = _configuration["Jwt:Key"] 
                     ?? Environment.GetEnvironmentVariable("JWT__Key") 
                     ?? Environment.GetEnvironmentVariable("NOOR_JWT_KEY");

        if (string.IsNullOrEmpty(keyStr))
            throw new InvalidOperationException("JWT Key is missing from configuration and environment variables.");

        var key = Encoding.UTF8.GetBytes(keyStr);
        var expiryDays = int.Parse(_configuration["Jwt:ExpiryDays"] ?? "1");
        var issuer   = _configuration["Jwt:Issuer"]   ?? "NoorPlatform";
        var audience = _configuration["Jwt:Audience"] ?? "NoorPlatformClients";

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email ?? ""),
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(ClaimTypes.Role, user.Role.ToString())
            }),
            Issuer = issuer,
            Audience = audience,
            Expires = DateTime.UtcNow.AddDays(expiryDays),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature
            )
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
}

public class LoginRequest
{
    [Required]
    public string Phone { get; set; } = string.Empty;

    [Required, MinLength(6)]
    public string Password { get; set; } = string.Empty;
}

public class ChangePasswordRequest
{
    [Required]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required, MinLength(8)]
    public string NewPassword { get; set; } = string.Empty;
}
