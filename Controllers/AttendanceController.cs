using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using CandidateAttendanceApp.Data;
using CandidateAttendanceApp.Helpers;
using CandidateAttendanceApp.Models;
using CandidateAttendanceApp.ViewModels;
using Microsoft.AspNetCore.Authorization;

namespace CandidateAttendanceApp.Controllers;

[Authorize(Roles = "User")]
public class AttendanceController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<IdentityUser> _userManager;

    public AttendanceController(ApplicationDbContext context, UserManager<IdentityUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index(int page = 1)
    {
        var userId = _userManager.GetUserId(User);

        var records = await _context.Attendances
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.Date)
            .ToListAsync();

        var paginatedRecords = ViewModels.PaginatedList<Attendance>.Create(records, page);
        return View(paginatedRecords);
    }

    [HttpPost]
    public async Task<IActionResult> CheckIn()
    {
        var userId = _userManager.GetUserId(User);
        
        if (string.IsNullOrEmpty(userId))
        {
            return RedirectToAction("Login", "Account");
        }

        var today = TimeHelper.TodayIst;

        var existingRecord = await _context.Attendances
            .FirstOrDefaultAsync(x => x.UserId == userId && x.Date == today);

        if (existingRecord == null)
        {
            _context.Attendances.Add(new Attendance
            {
                UserId = userId,
                Date = today,
                CheckIn = TimeHelper.NowIst
            });

            await _context.SaveChangesAsync();
            TempData["Success"] = "Check-in successful!";
        }
        else
        {
            TempData["Info"] = "You have already checked in today.";
        }

        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> CheckOut()
    {
        var userId = _userManager.GetUserId(User);
        
        if (string.IsNullOrEmpty(userId))
        {
            return RedirectToAction("Login", "Account");
        }

        var today = TimeHelper.TodayIst;

        var record = await _context.Attendances
            .FirstOrDefaultAsync(x => x.UserId == userId && x.Date == today);

        if (record != null && record.CheckIn != null && record.CheckOut == null)
        {
            record.CheckOut = TimeHelper.NowIst;
            record.WorkHours =
                Math.Round((decimal)(record.CheckOut.Value - record.CheckIn.Value).TotalHours, 2);

            await _context.SaveChangesAsync();
            TempData["Success"] = "Check-out successful!";
        }
        else if (record == null)
        {
            TempData["Error"] = "Please check in first.";
        }
        else if (record.CheckOut != null)
        {
            TempData["Info"] = "You have already checked out today.";
        }

        return RedirectToAction("Index");
    }

    public async Task<IActionResult> Dashboard()
    {
        var userId = _userManager.GetUserId(User);
        var today = TimeHelper.TodayIst;

        var record = await _context.Attendances
            .FirstOrDefaultAsync(a => a.UserId == userId && a.Date == today);

        ViewBag.CheckedIn = record?.CheckIn != null;
        ViewBag.WorkHours = record?.WorkHours ?? 0;

        // Calculate monthly attendance percentage
        var startOfMonth = new DateTime(today.Year, today.Month, 1);
        var totalDaysInMonth = DateTime.DaysInMonth(today.Year, today.Month);
        var currentDay = today.Day;
        
        // Count working days (excluding weekends) up to today
        var workingDays = 0;
        for (int day = 1; day <= currentDay; day++)
        {
            var date = new DateTime(today.Year, today.Month, day);
            if (date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday)
            {
                workingDays++;
            }
        }

        // Get attendance records for current month
        var monthlyRecords = await _context.Attendances
            .Where(a => a.UserId == userId && a.Date >= startOfMonth && a.Date <= today)
            .CountAsync();

        var attendancePercentage = workingDays > 0 ? Math.Round((decimal)monthlyRecords / workingDays * 100, 1) : 0;
        var absentPercentage = 100 - attendancePercentage;

        ViewBag.AttendancePercentage = attendancePercentage;
        ViewBag.AbsentPercentage = absentPercentage;
        ViewBag.PresentDays = monthlyRecords;
        ViewBag.WorkingDays = workingDays;

        return View();
    }

    public async Task<IActionResult> Monthly(int page = 1)
    {
        var userId = _userManager.GetUserId(User);

        var records = await _context.Attendances
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.Date)
            .ToListAsync();

        var paginatedRecords = ViewModels.PaginatedList<Attendance>.Create(records, page);
        return View(paginatedRecords);
    }

    public async Task<IActionResult> Fees(string? paymentStatus = null, int? courseId = null, int page = 1)
    {
        var userId = _userManager.GetUserId(User);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return RedirectToAction("Login", "Account");
        }

        ViewBag.PaymentStatus = NormalizePaymentStatus(paymentStatus);
        ViewBag.SelectedCourseId = courseId;

        return View(await BuildFeesViewModelAsync(userId, paymentStatus, courseId, page, 10));
    }

    private async Task<CandidateFeesViewModel> BuildFeesViewModelAsync(
        string userId,
        string? paymentStatus = null,
        int? courseId = null,
        int page = 1,
        int pageSize = 10)
    {
        var profile = await _context.UserProfiles
            .Include(candidateProfile => candidateProfile.Section)
            .FirstOrDefaultAsync(candidateProfile => candidateProfile.UserId == userId);

        var fees = await _context.Fees
            .Include(fee => fee.Course)
            .Where(fee => fee.UserId == userId)
            .OrderBy(fee => fee.IsPaid)
            .ThenBy(fee => fee.DueDate)
            .ThenByDescending(fee => fee.CreatedDate)
            .ToListAsync();

        var courses = fees
            .Where(fee => fee.CourseId.HasValue && fee.Course != null)
            .Select(fee => fee.Course!)
            .DistinctBy(course => course.Id)
            .OrderBy(course => course.Name)
            .ToList();

        var normalizedPaymentStatus = NormalizePaymentStatus(paymentStatus);
        if (!string.IsNullOrWhiteSpace(normalizedPaymentStatus))
        {
            fees = fees
                .Where(fee =>
                {
                    var isOverdue = !fee.IsPaid && fee.DueDate.Date < TimeHelper.TodayIst;

                    return normalizedPaymentStatus switch
                    {
                        "paid" => fee.IsPaid,
                        "pending" => !fee.IsPaid && !isOverdue,
                        "overdue" => isOverdue,
                        _ => true
                    };
                })
                .ToList();
        }

        if (courseId.HasValue)
        {
            fees = fees
                .Where(fee => fee.CourseId == courseId.Value)
                .ToList();
        }

        var totalRecords = fees.Count;
        var normalizedPage = Math.Max(1, page);
        var totalPages = totalRecords == 0 ? 0 : (int)Math.Ceiling(totalRecords / (double)pageSize);

        if (totalPages > 0 && normalizedPage > totalPages)
        {
            normalizedPage = totalPages;
        }

        var pagedFees = fees
            .Skip((normalizedPage - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new CandidateFeesViewModel
        {
            Fees = pagedFees,
            Profile = profile,
            Courses = courses,
            CurrentPage = normalizedPage,
            TotalPages = totalPages,
            PageSize = pageSize,
            TotalRecords = totalRecords,
            TotalPendingAmount = fees.Where(fee => !fee.IsPaid).Sum(fee => fee.Amount),
            TotalPaidAmount = fees.Where(fee => fee.IsPaid).Sum(fee => fee.Amount),
            PendingCount = fees.Count(fee => !fee.IsPaid),
            PaidCount = fees.Count(fee => fee.IsPaid)
        };
    }

    private string? NormalizePaymentStatus(string? paymentStatus)
    {
        var normalizedPaymentStatus = paymentStatus?.Trim().ToLowerInvariant();

        return normalizedPaymentStatus switch
        {
            "paid" => "paid",
            "pending" => "pending",
            "overdue" => "overdue",
            _ => null
        };
    }
}
