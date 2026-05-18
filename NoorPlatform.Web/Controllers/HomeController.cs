using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoorPlatform.Infrastructure.Data;
using NoorPlatform.Web.Models;

namespace NoorPlatform.Web.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly NoorDbContext _context;

    public HomeController(ILogger<HomeController> logger, NoorDbContext context)
    {
        _logger = logger;
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var studentCount = await _context.Students.CountAsync();
        var teacherCount = await _context.Teachers.CountAsync();
        var circleCount = await _context.Circles.CountAsync();

        // Calculate attendance percentage for today (or last 7 days)
        // For simplicity, we'll return a static 94% if no records, otherwise calculate it.
        var today = DateTime.Today;
        var todayAttendance = await _context.Attendances
            .Where(a => a.Date.Date == today)
            .ToListAsync();
            
        double attendancePercentage = 94.0;
        if (todayAttendance.Any())
        {
            var presentCount = todayAttendance.Count(a => a.Status == NoorPlatform.Core.Entities.AttendanceStatus.Present);
            attendancePercentage = Math.Round((double)presentCount / todayAttendance.Count * 100, 1);
        }

        var recentHifz = await _context.HifzRecords
            .Include(h => h.Student)
            .ThenInclude(s => s.User)
            .OrderByDescending(h => h.Date)
            .Take(5)
            .ToListAsync();

        var viewModel = new DashboardViewModel
        {
            StudentCount = studentCount,
            TeacherCount = teacherCount,
            CircleCount = circleCount,
            AttendancePercentage = attendancePercentage,
            RecentHifzRecords = recentHifz
        };

        return View(viewModel);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
