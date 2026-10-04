using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NoorPlatform.Infrastructure.Data;

namespace NoorPlatform.Api.Security;

public static class StudentQueryHelper
{
    public static IQueryable<Core.Entities.Student> ScopeForUser(
        NoorDbContext context,
        ClaimsPrincipal user)
    {
        var query = context.Students.AsQueryable();

        if (user.IsInRole("Admin"))
            return query;

        if (!user.IsInRole("Teacher"))
            return query.Where(_ => false);

        var userId = GetUserId(user);
        if (userId == null)
            return query.Where(_ => false);

        return query.Where(s =>
            (s.Circle != null && s.Circle.Teacher != null && s.Circle.Teacher.UserId == userId.Value) ||
            s.ExtraEnrollments.Any(e => e.Circle.Teacher != null && e.Circle.Teacher.UserId == userId.Value));
    }

    private static int? GetUserId(ClaimsPrincipal user)
    {
        var id = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(id, out var parsed) ? parsed : null;
    }
}
