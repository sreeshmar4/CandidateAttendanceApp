using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CandidateAttendanceApp.Data;
using CandidateAttendanceApp.Helpers;
using CandidateAttendanceApp.Models;

namespace CandidateAttendanceApp.Controllers;

[Authorize(Roles = "Admin")]
public class SettingsController : Controller
{
    private readonly ApplicationDbContext _context;

    public SettingsController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Sections(int page = 1)
    {
        var sections = await _context.Sections
            .OrderBy(s => s.Name)
            .ToListAsync();

        var paginatedSections = CandidateAttendanceApp.ViewModels.PaginatedList<Section>.Create(sections, page);
        return View(paginatedSections);
    }

    [HttpPost]
    public async Task<IActionResult> CreateSection(string name, decimal? fee)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["Error"] = "Course name is required.";
            return RedirectToAction("Sections");
        }

        if (fee.HasValue && fee.Value < 0)
        {
            TempData["Error"] = "Course fee cannot be negative.";
            return RedirectToAction("Sections");
        }

        var trimmedName = name.Trim();

        var existingSection = await _context.Sections
            .FirstOrDefaultAsync(s => s.Name.ToLower() == trimmedName.ToLower());

        if (existingSection != null)
        {
            TempData["Error"] = "A course with this name already exists.";
            return RedirectToAction("Sections");
        }

        var section = new Section
        {
            Name = trimmedName,
            Fee = fee,
            IsActive = true,
            CreatedDate = TimeHelper.NowIst
        };

        _context.Sections.Add(section);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Course created successfully!";
        return RedirectToAction("Sections");
    }

    [HttpPost]
    public async Task<IActionResult> UpdateSection(int id, string name, decimal? fee)
    {
        var section = await _context.Sections.FindAsync(id);
        if (section == null)
        {
            TempData["Error"] = "Course not found.";
            return RedirectToAction("Sections");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["Error"] = "Course name is required.";
            return RedirectToAction("Sections");
        }

        if (fee.HasValue && fee.Value < 0)
        {
            TempData["Error"] = "Course fee cannot be negative.";
            return RedirectToAction("Sections");
        }

        var trimmedName = name.Trim();
        var existingSection = await _context.Sections
            .FirstOrDefaultAsync(s => s.Id != id && s.Name.ToLower() == trimmedName.ToLower());

        if (existingSection != null)
        {
            TempData["Error"] = "A course with this name already exists.";
            return RedirectToAction("Sections");
        }

        section.Name = trimmedName;
        section.Fee = fee;

        await _context.SaveChangesAsync();

        TempData["Success"] = "Course updated successfully!";
        return RedirectToAction("Sections");
    }

    [HttpPost]
    public async Task<IActionResult> DeleteSection(int id)
    {
        var section = await _context.Sections.FindAsync(id);
        
        if (section != null)
        {
            // Check if any users are assigned to this course
            var usersInSection = await _context.UserProfiles
                .CountAsync(up => up.SectionId == id);

            if (usersInSection > 0)
            {
                TempData["Error"] = $"Cannot delete course. {usersInSection} user(s) are assigned to this course.";
            }
            else
            {
                _context.Sections.Remove(section);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Course deleted successfully!";
            }
        }

        return RedirectToAction("Sections");
    }

    [HttpPost]
    public async Task<IActionResult> ToggleSectionStatus(int id)
    {
        var section = await _context.Sections.FindAsync(id);
        
        if (section != null)
        {
            section.IsActive = !section.IsActive;
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Course {(section.IsActive ? "activated" : "deactivated")} successfully!";
        }

        return RedirectToAction("Sections");
    }
}
