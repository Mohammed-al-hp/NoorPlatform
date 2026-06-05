using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NoorPlatform.Infrastructure.Data;

namespace NoorPlatform.Api.Security;

public static class AuthorizationHelpers
{
    public static int? GetUserId(ClaimsPrincipal user)
    {
        var id = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(id, out var userId) ? userId : null;
    }

    public static async Task<bool> CanAccessStudentAsync(NoorDbContext context, ClaimsPrincipal user, int studentId)
    {
        if (user.IsInRole("Admin"))
            return true;

        var userId = GetUserId(user);
        if (userId == null)
            return false;

        if (user.IsInRole("Student"))
        {
            return await context.Students.AnyAsync(s => s.Id == studentId && s.UserId == userId);
        }

        if (user.IsInRole("Parent"))
        {
            return await context.Students.AnyAsync(s =>
                s.Id == studentId && s.Parent != null && s.Parent.UserId == userId);
        }

        // ─── تأكيد الحماية: المحفظ يرى فقط طلاب حلقته ───
        if (user.IsInRole("Teacher"))
        {
            return await context.Students.AnyAsync(s =>
                s.Id == studentId && s.Circle != null && s.Circle.Teacher != null &&
                s.Circle.Teacher.UserId == userId);
        }

        return false;
    }

    public static async Task<bool> CanAccessCircleAsync(NoorDbContext context, ClaimsPrincipal user, int circleId)
    {
        if (user.IsInRole("Admin"))
            return true;

        var userId = GetUserId(user);
        if (userId == null || !user.IsInRole("Teacher"))
            return false;

        return await context.Circles.AnyAsync(c =>
            c.Id == circleId && c.Teacher != null && c.Teacher.UserId == userId);
    }
}
