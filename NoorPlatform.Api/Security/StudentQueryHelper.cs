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
            return query;

        var userId = GetUserId(user);
        if (userId == null)
            return query.Where(_ => false);

        var circleIds = context.Circles
            .Where(c => c.Teacher != null && c.Teacher.UserId == userId.Value)
            .Select(c => c.Id);

        return query.Where(s => s.CircleId != null && circleIds.Contains(s.CircleId.Value));
    }

    private static int? GetUserId(ClaimsPrincipal user)
    {
        var id = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(id, out var parsed) ? parsed : null;
    }
}
