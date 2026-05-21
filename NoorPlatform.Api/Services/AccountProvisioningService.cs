using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NoorPlatform.Core.Entities;
using NoorPlatform.Infrastructure.Data;

namespace NoorPlatform.Api.Services;

public class AccountProvisioningService
{
    public const string DefaultTempPassword = "Noor@123";

    private readonly UserManager<User> _userManager;
    private readonly NoorDbContext _context;

    public AccountProvisioningService(UserManager<User> userManager, NoorDbContext context)
    {
        _userManager = userManager;
        _context = context;
    }

    public static string NormalizePhone(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return string.Empty;

        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("966"))
            return digits;
        if (digits.StartsWith("0"))
            return "966" + digits[1..];
        if (digits.Length == 9 && digits.StartsWith("5"))
            return "966" + digits;
        return digits;
    }

    public static string ToDisplayPhone(string normalized)
    {
        if (normalized.StartsWith("966") && normalized.Length >= 12)
            return "0" + normalized[3..];
        return normalized;
    }

    public async Task<(User User, string TempPassword, string? Error)> CreateUserAsync(
        string phone,
        string fullName,
        UserRole role,
        string? emailOverride = null)
    {
        var normalized = NormalizePhone(phone);
        if (normalized.Length < 11)
            return (null!, string.Empty, "رقم الهاتف غير صالح");

        if (await _userManager.FindByNameAsync(normalized) != null)
            return (null!, string.Empty, "رقم الهاتف مستخدم بالفعل");

        var email = emailOverride ?? $"{normalized}@noor.local";
        var user = new User
        {
            UserName = normalized,
            Email = email,
            PhoneNumber = normalized,
            FullName = fullName.Trim(),
            Role = role,
            EmailConfirmed = true,
            MustChangePassword = true,
            IsActive = true
        };

        var tempPassword = DefaultTempPassword;
        var result = await _userManager.CreateAsync(user, tempPassword);
        if (!result.Succeeded)
            return (null!, string.Empty, string.Join("، ", result.Errors.Select(e => e.Description)));

        return (user, tempPassword, null);
    }

    public async Task<(Parent? Parent, string? Error)> EnsureParentAsync(string parentName, string parentPhone)
    {
        var normalized = NormalizePhone(parentPhone);
        if (string.IsNullOrEmpty(normalized))
            return (null, null);

        var existing = await _context.Parents
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.Phone == normalized || p.User.UserName == normalized);

        if (existing != null)
            return (existing, null);

        var (parentUser, _, err) = await CreateUserAsync(
            normalized,
            string.IsNullOrWhiteSpace(parentName) ? "ولي أمر" : parentName.Trim(),
            UserRole.Parent);

        if (err != null)
            return (null, err);

        var parent = new Parent
        {
            UserId = parentUser.Id,
            Phone = normalized
        };
        _context.Parents.Add(parent);
        await _context.SaveChangesAsync();
        return (parent, null);
    }
}

public record AccountCredentialsDto(
    string FullName,
    string Phone,
    string DisplayPhone,
    string TempPassword,
    string Role,
    bool MustChangePassword);
