using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoorPlatform.Infrastructure.Data;
using NoorPlatform.Core.Entities;
using Microsoft.AspNetCore.Authorization;

namespace NoorPlatform.Web.Controllers;

[Authorize(Roles = "Admin")]
public class StudentsController : Controller
{
    private readonly NoorDbContext _context;

    public StudentsController(NoorDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var students = await _context.Students
            .Include(s => s.User)
            .Include(s => s.Circle)
            .Include(s => s.Parent)
            .ThenInclude(p => p!.User)
            .ToListAsync();
        return View(students);
    }

    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Create(Student student)
    {
        if (ModelState.IsValid)
        {
            _context.Add(student);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        return View(student);
    }
    
    // Additional CRUD actions (Edit, Delete, Details) would go here
}
