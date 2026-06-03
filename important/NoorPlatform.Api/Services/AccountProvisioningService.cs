using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NoorPlatform.Core.Entities;
using NoorPlatform.Infrastructure.Data;

namespace NoorPlatform.Api.Services;

public class AccountProvisioningService
{
    public static string GenerateSecurePassword(int length = 12)
    {
        const string lower = "abcdefghijklmnopqrstuvwxyz";
        const string upper = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        const string digits = "1234567890";
        const string specials = "!@#$%^&*";
        const string allChars = lower + upper + digits + specials;

        var res = new char[length];
        res[0] = lower[System.Security.Cryptography.RandomNumberGenerator.GetInt32(lower.Length)];
        res[1] = upper[System.Security.Cryptography.RandomNumberGenerator.GetInt32(upper.Length)];
        res[2] = digits[System.Security.Cryptography.RandomNumberGenerator.GetInt32(digits.Length)];
        res[3] = specials[System.Security.Cryptography.RandomNumberGenerator.GetInt32(specials.Length)];

        for (int i = 4; i < length; i++)
        {
            res[i] = allChars[System.Security.Cryptography.RandomNumberGenerator.GetInt32(allChars.Length)];
        }

        for (int i = res.Length - 1; i > 0; i--)
        {
            int j = System.Security.Cryptography.RandomNumberGenerator.GetInt32(i + 1);
            var temp = res[i];
            res[i] = res[j];
            res[j] = temp;
        }

        return new string(res);
    }

    private readonly UserManager<User> _userManager;
    private readonly NoorDbContext _context;

    public AccountProvisioningService(UserManager<User> userManager, NoorDbContext context)
    {
        _userManager = userManager;
        _context = context;
    }

    public static string NormalizePhone(string phone) => LibyanPhone.Normalize(phone);

    public static string ToDisplayPhone(string normalized) => LibyanPhone.ToDisplay(normalized);

    public async Task<(User User, string TempPassword, string? Error)> CreateUserAsync(
        string phone,
        string fullName,
        UserRole role,
        string? emailOverride = null)
    {
        var normalized = NormalizePhone(phone);
        if (!LibyanPhone.IsValid(phone))
            return (null!, string.Empty, "رقم الهاتف غير صالح. يجب أن يبدأ بـ 09 ويتكون من 10 أرقام");

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

        var tempPassword = GenerateSecurePassword();
        var result = await _userManager.CreateAsync(user, tempPassword);
        if (!result.Succeeded)
            return (null!, string.Empty, string.Join("، ", result.Errors.Select(e => e.Description)));

        return (user, tempPassword, null);
    }

    public async Task<(Parent? Parent, string? TempPassword, string? Error)> EnsureParentAsync(string parentName, string parentPhone)
    {
        var normalized = NormalizePhone(parentPhone);
        if (string.IsNullOrEmpty(normalized))
            return (null, null, null);

        var existing = await _context.Parents
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.Phone == normalized || p.User.UserName == normalized);

        if (existing != null)
            return (existing, null, null);

        var (parentUser, tempPassword, err) = await CreateUserAsync(
            normalized,
            string.IsNullOrWhiteSpace(parentName) ? "ولي أمر" : parentName.Trim(),
            UserRole.Parent);

        if (err != null)
            return (null, null, err);

        var parent = new Parent
        {
            UserId = parentUser.Id,
            Phone = normalized,
            User = parentUser
        };
        _context.Parents.Add(parent);
        await _context.SaveChangesAsync();
        return (parent, tempPassword, null);
    }
}

public record AccountCredentialsDto(
    string FullName,
    string Phone,
    string DisplayPhone,
    string TempPassword,
    string Role,
    bool MustChangePassword);
