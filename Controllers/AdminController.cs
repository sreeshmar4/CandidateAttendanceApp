using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CandidateAttendanceApp.Data;
using CandidateAttendanceApp.Helpers;
using CandidateAttendanceApp.Models;
using CandidateAttendanceApp.ViewModels;

namespace CandidateAttendanceApp.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<IdentityUser> _userManager;

    public AdminController(ApplicationDbContext context,
                           UserManager<IdentityUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<IActionResult> Dashboard(string? range = null)
    {
        var normalizedRange = NormalizeDashboardRange(range);
        var today = TimeHelper.TodayIst;
        var (startDate, endDate, rangeLabel) = GetDashboardRange(normalizedRange, today);
        var deletedUserIds = await _context.UserProfiles
            .Where(up => up.IsDeleted)
            .Select(up => up.UserId)
            .ToListAsync();
        var totalUsers = await _context.UserRoles
            .Join(_context.Roles,
                userRole => userRole.RoleId,
                role => role.Id,
                (userRole, role) => new { userRole.UserId, role.Name })
            .Where(item => item.Name == "User" && !deletedUserIds.Contains(item.UserId))
            .Select(item => item.UserId)
            .Distinct()
            .CountAsync();
        var totalRegistrations = totalUsers;
        var totalAdmissions = await _context.UserProfiles
            .Where(profile => !profile.IsDeleted &&
                ((profile.AdmissionNumber != null && profile.AdmissionNumber != string.Empty) ||
                profile.SectionId != null ||
                profile.AdmissionFee != null ||
                profile.StartDate != null))
            .Select(profile => profile.UserId)
            .Distinct()
            .CountAsync();
        var attendancesInRange = await _context.Attendances
            .Where(a => a.Date >= startDate && a.Date <= endDate)
            .ToListAsync();
        var presentAttendances = attendancesInRange
            .Where(a => a.CheckIn != null)
            .ToList();

        var chartPoints = BuildDashboardChartPoints(normalizedRange, startDate, endDate, presentAttendances);

        return View(new AdminDashboardViewModel
        {
            SelectedRange = normalizedRange,
            RangeLabel = rangeLabel,
            TotalUsers = totalUsers,
            TotalRegistrations = totalRegistrations,
            TotalAdmissions = totalAdmissions,
            PresentCount = presentAttendances.Count,
            StartDate = startDate,
            EndDate = endDate,
            ChartLabels = chartPoints.Select(point => point.Label).ToList(),
            ChartValues = chartPoints.Select(point => point.Value).ToList()
        });
    }

    public async Task<IActionResult> UserSummary(string? searchTerm, int page = 1)
    {
        // Get all users in User role
        var users = await _userManager.GetUsersInRoleAsync("User");

        // Get user profiles with sections (exclude soft-deleted)
        var allProfiles = await _context.UserProfiles
            .Include(up => up.Section)
            .Where(up => users.Select(u => u.Id).Contains(up.UserId))
            .ToDictionaryAsync(up => up.UserId);

        // Filter out users whose profiles are soft-deleted
        var deletedUserIds = allProfiles
            .Where(kvp => kvp.Value.IsDeleted)
            .Select(kvp => kvp.Key)
            .ToHashSet();

        users = users.Where(u => !deletedUserIds.Contains(u.Id)).ToList();

        var userProfiles = allProfiles
            .Where(kvp => !kvp.Value.IsDeleted)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var normalizedSearchTerm = searchTerm.Trim();

            users = users
                .Where(user =>
                {
                    if (!userProfiles.TryGetValue(user.Id, out var profile))
                    {
                        return false;
                    }

                    return (!string.IsNullOrWhiteSpace(profile.Name) &&
                            profile.Name.Contains(normalizedSearchTerm, StringComparison.OrdinalIgnoreCase)) ||
                           (!string.IsNullOrWhiteSpace(profile.AdmissionNumber) &&
                            profile.AdmissionNumber == (normalizedSearchTerm));
                })
                .ToList();

            ViewBag.SearchTerm = normalizedSearchTerm;
        }

        ViewBag.UserProfiles = userProfiles;

        var paginatedUsers = ViewModels.PaginatedList<IdentityUser>.Create(users.ToList(), page);
        return View(paginatedUsers);
    }

    public async Task<IActionResult> UserReport(string userId, int page = 1)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return NotFound();
        }

        var attendances = await _context.Attendances
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.Date)
            .ToListAsync();

        ViewBag.UserEmail = user.Email;
        ViewBag.UserId = userId;

        var paginatedAttendances = ViewModels.PaginatedList<Attendance>.Create(attendances, page);
        return View(paginatedAttendances);
    }

    [HttpPost]
    public async Task<IActionResult> ToggleLate(int attendanceId, string userId)
    {
        var attendance = await _context.Attendances.FindAsync(attendanceId);
        
        if (attendance != null)
        {
            attendance.IsLate = !attendance.IsLate;
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Attendance marked as {(attendance.IsLate ? "Late" : "On-Time")}";
        }

        return RedirectToAction("UserReport", new { userId = userId });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteUser(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return NotFound();
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return NotFound();
        }

        var profile = await _context.UserProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId);

        if (profile == null)
        {
            profile = new UserProfile
            {
                UserId = userId,
                Name = user.Email ?? string.Empty,
                AdmissionNumber = string.Empty,
                IsDeleted = true
            };
            _context.UserProfiles.Add(profile);
        }
        else
        {
            profile.IsDeleted = true;
        }

        await _context.SaveChangesAsync();

        TempData["Success"] = "User has been deleted successfully.";
        return RedirectToAction(nameof(UserSummary));
    }

    public IActionResult CreateUser()
    {
        return RedirectToAction(nameof(NewAdmission));
    }

    public async Task<IActionResult> NewAdmission()
    {
        var model = new AdminUserFormViewModel
        {
            IsRegistrationMode = false,
            IsAdmissionLookupComplete = false
        };

        await PopulateUserFormViewDataAsync(model);
        return View("CreateUser", model);
    }

    public async Task<IActionResult> NewRegistration()
    {
        var model = new AdminUserFormViewModel
        {
            IsRegistrationMode = true
        };

        await PopulateUserFormViewDataAsync(model);
        return View("CreateUser", model);
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser(AdminUserFormViewModel model, string? submitAction = null)
    {
        model.IsRegistrationMode = false;
        return await NewAdmission(model, submitAction);
    }

    [HttpPost]
    public async Task<IActionResult> NewAdmission(AdminUserFormViewModel model, string? submitAction = null)
    {
        model.IsRegistrationMode = false;
        model.IsEditMode = false;
        NormalizeUserFormModel(model);

        if (string.Equals(submitAction, "lookup", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(model.UserId))
        {
            return await LoadAdmissionCandidateAsync(model);
        }

        return await UpdateAdmissionAsync(model);
    }

    [HttpPost]
    public async Task<IActionResult> NewRegistration(AdminUserFormViewModel model)
    {
        model.IsRegistrationMode = true;
        return await CreateUserCoreAsync(model);
    }

    public async Task<IActionResult> EditUser(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return NotFound();
        }

        var profile = await _context.UserProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.UserId == userId);

        var model = new AdminUserFormViewModel
        {
            UserId = user.Id,
            IsEditMode = true,
            IsRegistrationMode = false,
            Name = profile?.Name ?? string.Empty,
            Email = user.Email ?? string.Empty,
            AdmissionNumber = profile?.AdmissionNumber ?? string.Empty,
            CourseFee = profile?.CourseFee,
            AdmissionFee = profile?.AdmissionFee,
            StartDate = profile?.StartDate,
            PhoneNumber = profile?.PhoneNumber,
            ParentName = profile?.ParentName,
            ParentOccupation = profile?.ParentOccupation,
            ParentNo = profile?.ParentNo,
            Address = profile?.Address,
            EducationalQualification = profile?.EducationalQualification,
            CollegeName = profile?.CollegeName,
            YearOfPassout = profile?.YearOfPassout,
            Department = profile?.Department,
            CareerConsultant = profile?.CareerConsultant,
            HowFoundSmec = profile?.HowFoundSmec,
            BranchLocation = profile?.BranchLocation,
            SkillSector = profile?.SkillSector,
            CourseWithFee = profile?.CourseWithFee,
            ApplicationFee = profile?.ApplicationFee,
            ApplicationFeeStatus = profile?.ApplicationFeeStatus,
            TentativeStartMonth = profile?.TentativeStartMonth,
            SectionId = profile?.SectionId
        };

        // If AdmissionFee was never set but we have ApplicationFee from registration, use it
        if (model.AdmissionFee == null && model.ApplicationFee != null)
            model.AdmissionFee = model.ApplicationFee;

        await PopulateUserFormViewDataAsync(model);

        return View("CreateUser", model);
    }

    [HttpPost]
    public async Task<IActionResult> EditUser(AdminUserFormViewModel model)
    {
        model.IsEditMode = true;
        model.IsRegistrationMode = false;
        NormalizeUserFormModel(model);
        await PopulateUserFormViewDataAsync(model);

        if (string.IsNullOrWhiteSpace(model.UserId))
        {
            return NotFound();
        }

        var user = await _userManager.FindByIdAsync(model.UserId);
        if (user == null)
        {
            return NotFound();
        }

        if (!await ValidateUserFormAsync(model, requirePassword: false, currentUserId: user.Id))
        {
            return View("CreateUser", model);
        }

        user.Email = model.Email;
        user.UserName = model.Email;

        var updateUserResult = await _userManager.UpdateAsync(user);
        if (!updateUserResult.Succeeded)
        {
            foreach (var error in updateUserResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View("CreateUser", model);
        }

        if (!string.IsNullOrWhiteSpace(model.Password))
        {
            var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
            var resetPasswordResult = await _userManager.ResetPasswordAsync(user, resetToken, model.Password);

            if (!resetPasswordResult.Succeeded)
            {
                foreach (var error in resetPasswordResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }

                return View("CreateUser", model);
            }
        }

        var userProfile = await _context.UserProfiles
            .FirstOrDefaultAsync(profile => profile.UserId == user.Id);

        if (userProfile == null)
        {
            userProfile = new UserProfile
            {
                UserId = user.Id
            };
            _context.UserProfiles.Add(userProfile);
        }

        userProfile.Name = model.Name;
        userProfile.AdmissionNumber = model.AdmissionNumber;
        userProfile.CourseFee = model.CourseFee;
        userProfile.StartDate = model.StartDate?.Date;
        userProfile.PhoneNumber = model.PhoneNumber;
        userProfile.ParentName = model.ParentName;
        userProfile.ParentOccupation = model.ParentOccupation;
        userProfile.ParentNo = model.ParentNo;
        userProfile.Address = model.Address;
        userProfile.EducationalQualification = model.EducationalQualification;
        userProfile.CollegeName = model.CollegeName;
        userProfile.YearOfPassout = model.YearOfPassout;
        userProfile.Department = model.Department;
        userProfile.CareerConsultant = model.CareerConsultant;
        userProfile.HowFoundSmec = model.HowFoundSmec;
        userProfile.BranchLocation = model.BranchLocation;
        userProfile.SkillSector = model.SkillSector;
        userProfile.CourseWithFee = model.CourseWithFee;
        userProfile.ApplicationFee = model.ApplicationFee;
        userProfile.ApplicationFeeStatus = model.ApplicationFeeStatus;
        userProfile.TentativeStartMonth = model.TentativeStartMonth;
        userProfile.SectionId = model.SectionId;

        // Auto-set course fee from selected course
        if (model.SectionId.HasValue)
        {
            var selectedSection = await _context.Sections
                .FirstOrDefaultAsync(s => s.Id == model.SectionId.Value && s.IsActive);
            userProfile.AdmissionFee = selectedSection?.Fee;
        }
        else
        {
            userProfile.AdmissionFee = null;
        }

        await _context.SaveChangesAsync();

        TempData["Success"] = "User details updated successfully!";
        return RedirectToAction(nameof(UserSummary));
    }

    public async Task<IActionResult> Fees(string? status = null)
    {
        ApplyFeesStatusMessage(status);

        return View(await BuildFeesViewModelAsync());
    }

    public async Task<IActionResult> FeeRecords(string? status = null, string? searchTerm = null, string? paymentStatus = null, int? courseId = null, int page = 1)
    {
        ApplyFeesStatusMessage(status);
        ViewBag.SearchTerm = searchTerm?.Trim();
        ViewBag.PaymentStatus = NormalizePaymentStatus(paymentStatus);
        ViewBag.SelectedCourseId = courseId;
        ViewBag.CurrentPage = page;

        return View(await BuildFeesViewModelAsync(searchTerm, paymentStatus, courseId, page, 10, true));
    }

    public async Task<IActionResult> CollectFee(string? admissionNumber = null)
    {
        var model = new CollectFeeViewModel();
        model.Courses = await _context.Sections
            .Where(s => s.IsActive)
            .OrderBy(s => s.Name)
            .ToListAsync();

        if (!string.IsNullOrWhiteSpace(admissionNumber))
        {
            model.AdmissionNumber = admissionNumber.Trim();
        }

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> LookupCandidate(string admissionNumber)
    {
        if (string.IsNullOrWhiteSpace(admissionNumber))
            return Json(new { found = false });

        var profile = await _context.UserProfiles
            .Include(p => p.Section)
            .FirstOrDefaultAsync(p => p.AdmissionNumber == admissionNumber.Trim());

        if (profile == null)
            return Json(new { found = false });

        var user = await _userManager.FindByIdAsync(profile.UserId);

        // Total collected = registration fee + all fee records for this user
        var totalFeeRecords = await _context.Fees
            .Where(f => f.UserId == profile.UserId)
            .SumAsync(f => f.Amount);
        var registrationFee = profile.CourseFee ?? 0;
        var totalCollected = registrationFee + totalFeeRecords;

        return Json(new
        {
            found = true,
            userId = profile.UserId,
            candidateName = profile.Name,
            admissionNumber = profile.AdmissionNumber,
            courseId = profile.SectionId,
            totalCollected = totalCollected
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetCourseFeeInfo(string userId, int courseId)
    {
        var course = await _context.Sections
            .FirstOrDefaultAsync(s => s.Id == courseId && s.IsActive);

        decimal courseFee = course?.Fee ?? 0;

        decimal totalCollected = 0;
        if (!string.IsNullOrWhiteSpace(userId))
        {
            var profile = await _context.UserProfiles
                .FirstOrDefaultAsync(p => p.UserId == userId);
            var registrationFee = profile?.CourseFee ?? 0;
            var totalFeeRecords = await _context.Fees
                .Where(f => f.UserId == userId)
                .SumAsync(f => f.Amount);
            totalCollected = registrationFee + totalFeeRecords;
        }

        return Json(new
        {
            courseFee = courseFee,
            totalCollected = totalCollected
        });
    }

    [HttpPost]
    public async Task<IActionResult> CollectFee(CollectFeeViewModel model)
    {
        model.Courses = await _context.Sections
            .Where(s => s.IsActive)
            .OrderBy(s => s.Name)
            .ToListAsync();

        // Submit fee collection
        if (string.IsNullOrWhiteSpace(model.UserId) || !model.CourseId.HasValue || !model.DueDate.HasValue)
        {
            ModelState.AddModelError(string.Empty, "Please load a candidate and fill all required fields.");
            return View(model);
        }

        var course = await _context.Sections
            .FirstOrDefaultAsync(s => s.Id == model.CourseId.Value && s.IsActive);

        if (course == null)
        {
            ModelState.AddModelError(nameof(model.CourseId), "Selected course is invalid.");
            return View(model);
        }

        var feeAmount = model.Amount;
        if (feeAmount is null || feeAmount <= 0)
        {
            ModelState.AddModelError(nameof(model.Amount), "A valid fee amount is required.");
            return View(model);
        }

        var fee = new Fee
        {
            UserId = model.UserId,
            Title = course.Name,
            CourseId = course.Id,
            Amount = feeAmount.Value,
            DueDate = model.DueDate.Value.Date,
            IsPaid = false,
            PaidDate = null,
            Notes = string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes.Trim(),
            CreatedDate = TimeHelper.NowIst
        };

        _context.Fees.Add(fee);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Fee collected successfully.";
        return RedirectToAction(nameof(Fees));
    }

    [HttpPost]
    public async Task<IActionResult> CreateFee(string userId, int? courseId, decimal? amount, DateTime? dueDate, string? notes)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userId) ||
                courseId is null ||
                dueDate is null)
            {
                return RedirectToAction(nameof(Fees), new { status = "validation_error" });
            }

            var candidateUsers = await GetCandidateUsersAsync();
            var user = candidateUsers.FirstOrDefault(candidate => candidate.Id == userId);
            if (user == null)
            {
                return RedirectToAction(nameof(Fees), new { status = "invalid_user" });
            }

            var course = await _context.Sections
                .FirstOrDefaultAsync(section => section.Id == courseId.Value && section.IsActive);

            if (course == null)
            {
                return RedirectToAction(nameof(Fees), new { status = "invalid_course" });
            }

            var userProfile = await _context.UserProfiles
                .FirstOrDefaultAsync(profile => profile.UserId == userId);

            var feeAmount = amount;
            if (feeAmount is null || feeAmount <= 0)
            {
                feeAmount = course.Fee;
            }

            if (feeAmount is null || feeAmount <= 0)
            {
                feeAmount = userProfile?.CourseFee;
            }

            if (feeAmount is null || feeAmount <= 0)
            {
                return RedirectToAction(nameof(Fees), new { status = "validation_error" });
            }

            var normalizedNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
            var fee = new Fee
            {
                UserId = userId,
                Title = course.Name,
                CourseId = course.Id,
                Amount = feeAmount.Value,
                DueDate = dueDate.Value.Date,
                IsPaid = false,
                PaidDate = null,
                Notes = normalizedNotes,
                CreatedDate = TimeHelper.NowIst
            };

            _context.Fees.Add(fee);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Fees), new { status = "fee_created" });
        }
        catch
        {
            return RedirectToAction(nameof(Fees), new { status = "fee_error" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> ToggleFeeStatus(int id, string? returnAction = null, string? searchTerm = null, string? paymentStatus = null, int? courseId = null, int page = 1)
    {
        try
        {
            var fee = await _context.Fees.FindAsync(id);
            if (fee == null)
            {
                return RedirectToFeeDestination(returnAction, "fee_missing", searchTerm, paymentStatus, courseId, page);
            }

            fee.IsPaid = !fee.IsPaid;
            fee.PaidDate = fee.IsPaid ? TimeHelper.NowIst : null;

            await _context.SaveChangesAsync();

            return RedirectToFeeDestination(returnAction, fee.IsPaid ? "fee_paid" : "fee_pending", searchTerm, paymentStatus, courseId, page);
        }
        catch
        {
            return RedirectToFeeDestination(returnAction, "fee_error", searchTerm, paymentStatus, courseId, page);
        }
    }

    [HttpPost]
    public async Task<IActionResult> DeleteFee(int id, string? returnAction = null, string? searchTerm = null, string? paymentStatus = null, int? courseId = null, int page = 1)
    {
        try
        {
            var fee = await _context.Fees.FindAsync(id);
            if (fee == null)
            {
                return RedirectToFeeDestination(returnAction, "fee_missing", searchTerm, paymentStatus, courseId, page);
            }

            _context.Fees.Remove(fee);
            await _context.SaveChangesAsync();

            return RedirectToFeeDestination(returnAction, "fee_deleted", searchTerm, paymentStatus, courseId, page);
        }
        catch
        {
            return RedirectToFeeDestination(returnAction, "fee_error", searchTerm, paymentStatus, courseId, page);
        }
    }

    private async Task<AdminFeesViewModel> BuildFeesViewModelAsync(
        string? searchTerm = null,
        string? paymentStatus = null,
        int? courseId = null,
        int page = 1,
        int pageSize = 10,
        bool paginate = false)
    {
        var users = await GetCandidateUsersAsync();
        var userIds = users.Select(user => user.Id).ToList();
        var courses = await _context.Sections
            .Where(section => section.IsActive)
            .OrderBy(section => section.Name)
            .ToListAsync();

        var userProfiles = await _context.UserProfiles
            .Include(profile => profile.Section)
            .Where(profile => userIds.Contains(profile.UserId))
            .ToDictionaryAsync(profile => profile.UserId);

        var fees = new List<Fee>();

        try
        {
            fees = await _context.Fees
                .Include(fee => fee.User)
                .Include(fee => fee.Course)
                .Where(fee => userIds.Contains(fee.UserId))
                .OrderBy(fee => fee.IsPaid)
                .ThenBy(fee => fee.DueDate)
                .ThenByDescending(fee => fee.CreatedDate)
                .ToListAsync();
        }
        catch
        {
            // Show candidate data even if the fees table is not ready yet.
        }

        var normalizedSearchTerm = searchTerm?.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedSearchTerm))
        {
            fees = fees
                .Where(fee =>
                {
                    if (!userProfiles.TryGetValue(fee.UserId, out var profile))
                    {
                        return false;
                    }

                    return (!string.IsNullOrWhiteSpace(profile.Name) &&
                            profile.Name.Contains(normalizedSearchTerm, StringComparison.OrdinalIgnoreCase)) ||
                           (!string.IsNullOrWhiteSpace(profile.AdmissionNumber) &&
                            profile.AdmissionNumber.Contains(normalizedSearchTerm, StringComparison.OrdinalIgnoreCase));
                })
                .ToList();
        }

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
                .Where(fee => ResolveFeeCourseId(fee, userProfiles) == courseId.Value)
                .ToList();
        }

        var totalRecords = fees.Count;
        var normalizedPage = Math.Max(1, page);
        var totalPages = totalRecords == 0 ? 0 : (int)Math.Ceiling(totalRecords / (double)pageSize);

        if (paginate && totalPages > 0 && normalizedPage > totalPages)
        {
            normalizedPage = totalPages;
        }

        if (paginate)
        {
            fees = fees
                .Skip((normalizedPage - 1) * pageSize)
                .Take(pageSize)
                .ToList();
        }

        return new AdminFeesViewModel
        {
            Fees = fees,
            Users = users.OrderBy(user => user.Email).ToList(),
            Courses = courses,
            UserProfiles = userProfiles,
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

    private IActionResult RedirectToFeeDestination(string? returnAction, string status, string? searchTerm = null, string? paymentStatus = null, int? courseId = null, int page = 1)
    {
        if (string.Equals(returnAction, nameof(FeeRecords), StringComparison.OrdinalIgnoreCase))
        {
            return RedirectToAction(nameof(FeeRecords), new { status, searchTerm, paymentStatus, courseId, page });
        }

        return RedirectToAction(nameof(Fees), new { status });
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

    private void ApplyFeesStatusMessage(string? status)
    {
        switch (status)
        {
            case "fee_created":
                ViewBag.Success = "Fee added successfully.";
                break;
            case "fee_paid":
                ViewBag.Success = "Fee marked as paid.";
                break;
            case "fee_pending":
                ViewBag.Success = "Fee moved back to pending.";
                break;
            case "fee_deleted":
                ViewBag.Success = "Fee deleted successfully.";
                break;
            case "validation_error":
                ViewBag.Error = "User, course name, valid fee amount, and due date are required.";
                break;
            case "invalid_user":
                ViewBag.Error = "Selected user is invalid.";
                break;
            case "invalid_course":
                ViewBag.Error = "Selected course is invalid.";
                break;
            case "fee_missing":
                ViewBag.Error = "Fee record not found.";
                break;
            case "fee_error":
                ViewBag.Error = "Fee could not be saved right now. Please try again.";
                break;
        }
    }

    private async Task<IActionResult> CreateUserCoreAsync(AdminUserFormViewModel model)
    {
        model.IsEditMode = false;
        NormalizeUserFormModel(model);
        await PopulateUserFormViewDataAsync(model);

        if (!await ValidateUserFormAsync(
                model,
                requirePassword: !model.IsRegistrationMode,
                requireAdmissionNumber: false))
        {
            return View("CreateUser", model);
        }

        var password = model.IsRegistrationMode && string.IsNullOrWhiteSpace(model.Password)
            ? GenerateTemporaryPassword()
            : model.Password;

        var user = new IdentityUser
        {
            UserName = model.Email,
            Email = model.Email,
            EmailConfirmed = true
        };

        var result = await _userManager.CreateAsync(user, password);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View("CreateUser", model);
        }

        await _userManager.AddToRoleAsync(user, "User");

        // Auto-set course fee from selected course
        if (!model.IsRegistrationMode && model.SectionId.HasValue)
        {
            var selectedSection = await _context.Sections
                .FirstOrDefaultAsync(s => s.Id == model.SectionId.Value && s.IsActive);
            if (selectedSection != null)
            {
                model.AdmissionFee = selectedSection.Fee;
            }
        }

        var userProfile = new UserProfile
        {
            UserId = user.Id,
            Name = model.Name,
            AdmissionNumber = model.IsRegistrationMode ? string.Empty : model.AdmissionNumber,
            CourseFee = model.CourseFee,
            AdmissionFee = model.AdmissionFee,
            StartDate = model.StartDate?.Date,
            PhoneNumber = model.PhoneNumber,
            ParentNo = model.ParentNo,
            Address = model.Address,
            EducationalQualification = model.EducationalQualification,
            SectionId = model.IsRegistrationMode ? null : model.SectionId
        };

        _context.UserProfiles.Add(userProfile);
        await _context.SaveChangesAsync();

        TempData["Success"] = model.IsRegistrationMode
            ? $"Registration created successfully! Temporary password: {password}"
            : "Admission created successfully!";

        return RedirectToAction(nameof(UserSummary));
    }

    private async Task<List<IdentityUser>> GetCandidateUsersAsync()
    {
        var userRoleIds = await (
            from userRole in _context.UserRoles
            join role in _context.Roles on userRole.RoleId equals role.Id
            where role.Name == "User"
            select userRole.UserId)
            .ToListAsync();

        var profileUserIds = await _context.UserProfiles
            .Select(profile => profile.UserId)
            .ToListAsync();

        var candidateIds = userRoleIds
            .Concat(profileUserIds)
            .Distinct()
            .ToList();

        if (candidateIds.Count == 0)
        {
            return new List<IdentityUser>();
        }

        return await _userManager.Users
            .Where(user => candidateIds.Contains(user.Id))
            .OrderBy(user => user.Email)
            .ToListAsync();
    }

    private async Task<IActionResult> LoadAdmissionCandidateAsync(AdminUserFormViewModel model)
    {
        model.IsAdmissionLookupComplete = false;
        await PopulateUserFormViewDataAsync(model);

        if (string.IsNullOrWhiteSpace(model.PhoneNumber))
        {
            ModelState.AddModelError(nameof(model.PhoneNumber), "Phone number is required to load registration details.");
            return View("CreateUser", model);
        }

        var profile = await _context.UserProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.PhoneNumber == model.PhoneNumber);

        if (profile == null)
        {
            ModelState.AddModelError(nameof(model.PhoneNumber), "No registration was found for this phone number.");
            return View("CreateUser", model);
        }

        var user = await _userManager.FindByIdAsync(profile.UserId);
        if (user == null)
        {
            ModelState.AddModelError(nameof(model.PhoneNumber), "Registration details are not available for this phone number.");
            return View("CreateUser", model);
        }

        model.UserId = user.Id;
        model.Email = user.Email ?? model.Email;
        model.Name = profile.Name;
        model.AdmissionNumber = profile.AdmissionNumber;
        model.Address = profile.Address;
        model.PhoneNumber = profile.PhoneNumber;
        model.ParentName = profile.ParentName;
        model.ParentOccupation = profile.ParentOccupation;
        model.ParentNo = profile.ParentNo;
        model.EducationalQualification = profile.EducationalQualification;
        model.CollegeName = profile.CollegeName;
        model.YearOfPassout = profile.YearOfPassout;
        model.Department = profile.Department;
        model.CareerConsultant = profile.CareerConsultant;
        model.HowFoundSmec = profile.HowFoundSmec;
        model.BranchLocation = profile.BranchLocation;
        model.SkillSector = profile.SkillSector;
        model.CourseWithFee = profile.CourseWithFee;
        model.ApplicationFee = profile.ApplicationFee;
        model.ApplicationFeeStatus = profile.ApplicationFeeStatus;
        model.TentativeStartMonth = profile.TentativeStartMonth;
        model.StartDate = profile.StartDate;
        model.SectionId = profile.SectionId;
        model.AdmissionFee = profile.AdmissionFee ?? profile.ApplicationFee;
        model.CourseFee = profile.CourseFee;
        model.IsAdmissionLookupComplete = true;
        ModelState.Clear();

        return View("CreateUser", model);
    }

    private async Task<IActionResult> UpdateAdmissionAsync(AdminUserFormViewModel model)
    {
        model.IsAdmissionLookupComplete = true;
        await PopulateUserFormViewDataAsync(model);

        if (string.IsNullOrWhiteSpace(model.UserId))
        {
            ModelState.AddModelError(string.Empty, "Load an existing registration by phone number before updating admission details.");
            return View("CreateUser", model);
        }

        var user = await _userManager.FindByIdAsync(model.UserId);
        if (user == null)
        {
            ModelState.AddModelError(string.Empty, "The selected registration could not be found.");
            return View("CreateUser", model);
        }

        if (!await ValidateUserFormAsync(model, requirePassword: false, currentUserId: user.Id, requireAdmissionNumber: true))
        {
            return View("CreateUser", model);
        }

        // Auto-set course fee from selected course
        if (model.SectionId.HasValue)
        {
            var selectedSection = await _context.Sections
                .FirstOrDefaultAsync(s => s.Id == model.SectionId.Value && s.IsActive);
            if (selectedSection != null)
            {
                model.AdmissionFee = selectedSection.Fee;
            }
        }

        user.Email = model.Email;
        user.UserName = model.Email;

        var updateUserResult = await _userManager.UpdateAsync(user);
        if (!updateUserResult.Succeeded)
        {
            foreach (var error in updateUserResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View("CreateUser", model);
        }

        var userProfile = await _context.UserProfiles
            .FirstOrDefaultAsync(profile => profile.UserId == user.Id);

        if (userProfile == null)
        {
            userProfile = new UserProfile
            {
                UserId = user.Id
            };
            _context.UserProfiles.Add(userProfile);
        }

        userProfile.Name = model.Name;
        userProfile.AdmissionNumber = model.AdmissionNumber;
        userProfile.Address = model.Address;
        userProfile.PhoneNumber = model.PhoneNumber;
        userProfile.ParentName = model.ParentName;
        userProfile.ParentOccupation = model.ParentOccupation;
        userProfile.ParentNo = model.ParentNo;
        userProfile.EducationalQualification = model.EducationalQualification;
        userProfile.CollegeName = model.CollegeName;
        userProfile.YearOfPassout = model.YearOfPassout;
        userProfile.Department = model.Department;
        userProfile.CareerConsultant = model.CareerConsultant;
        userProfile.HowFoundSmec = model.HowFoundSmec;
        userProfile.BranchLocation = model.BranchLocation;
        userProfile.SkillSector = model.SkillSector;
        userProfile.CourseWithFee = model.CourseWithFee;
        userProfile.ApplicationFee = model.ApplicationFee;
        userProfile.ApplicationFeeStatus = model.ApplicationFeeStatus;
        userProfile.TentativeStartMonth = model.TentativeStartMonth;
        userProfile.StartDate = model.StartDate?.Date;
        userProfile.SectionId = model.SectionId;
        userProfile.AdmissionFee = model.AdmissionFee;
        userProfile.CourseFee = model.CourseFee;

        await _context.SaveChangesAsync();

        TempData["Success"] = "Admission updated successfully!";
        return RedirectToAction(nameof(UserSummary));
    }

    private async Task PopulateUserFormViewDataAsync(AdminUserFormViewModel model)
    {
        model.Sections = await _context.Sections
            .Where(section => section.IsActive)
            .OrderBy(section => section.Name)
            .ToListAsync();
    }

    private async Task<bool> ValidateUserFormAsync(
        AdminUserFormViewModel model,
        bool requirePassword,
        string? currentUserId = null,
        bool requireAdmissionNumber = true)
    {
        if (string.IsNullOrWhiteSpace(model.Name))
        {
            ModelState.AddModelError(nameof(model.Name), "Name is required.");
        }

        if (string.IsNullOrWhiteSpace(model.Email))
        {
            ModelState.AddModelError(nameof(model.Email), "Email is required.");
        }

        if (requireAdmissionNumber && string.IsNullOrWhiteSpace(model.AdmissionNumber))
        {
            ModelState.AddModelError(nameof(model.AdmissionNumber), "Admission number is required.");
        }

        if (requirePassword && string.IsNullOrWhiteSpace(model.Password))
        {
            ModelState.AddModelError(nameof(model.Password), "Password is required.");
        }

        var hasPasswordInput =
            !string.IsNullOrWhiteSpace(model.Password) ||
            !string.IsNullOrWhiteSpace(model.ConfirmPassword);

        if ((requirePassword || hasPasswordInput) &&
            !string.Equals(model.Password, model.ConfirmPassword, StringComparison.Ordinal))
        {
            ModelState.AddModelError(nameof(model.ConfirmPassword), "Passwords do not match.");
        }

        if (!model.IsRegistrationMode && model.SectionId.HasValue)
        {
            var sectionExists = await _context.Sections
                .AnyAsync(section => section.Id == model.SectionId.Value && section.IsActive);

            if (!sectionExists)
            {
                ModelState.AddModelError(nameof(model.SectionId), "Selected course is invalid.");
            }
        }

        if (!string.IsNullOrWhiteSpace(model.Email))
        {
            var existingUser = await _userManager.FindByEmailAsync(model.Email);
            if (existingUser != null &&
                !string.Equals(existingUser.Id, currentUserId, StringComparison.Ordinal))
            {
                ModelState.AddModelError(nameof(model.Email), "A user with this email already exists.");
            }
        }

        if (!string.IsNullOrWhiteSpace(model.AdmissionNumber))
        {
            var existingAdmissionNumber = await _context.UserProfiles
                .AnyAsync(profile =>
                    profile.AdmissionNumber == model.AdmissionNumber &&
                    profile.UserId != currentUserId);

            if (existingAdmissionNumber)
            {
                ModelState.AddModelError(nameof(model.AdmissionNumber), "This admission number is already assigned to another user.");
            }
        }

        return ModelState.IsValid;
    }

    private static int? ResolveFeeCourseId(Fee fee, IReadOnlyDictionary<string, UserProfile> userProfiles)
    {
        if (fee.CourseId.HasValue)
        {
            return fee.CourseId.Value;
        }

        if (userProfiles.TryGetValue(fee.UserId, out var profile))
        {
            return profile.SectionId;
        }

        return null;
    }

    private static void NormalizeUserFormModel(AdminUserFormViewModel model)
    {
        model.Name = model.Name?.Trim() ?? string.Empty;
        model.Email = model.Email?.Trim() ?? string.Empty;
        model.AdmissionNumber = model.AdmissionNumber?.Trim() ?? string.Empty;
        model.PhoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber.Trim();
        model.ParentName = string.IsNullOrWhiteSpace(model.ParentName) ? null : model.ParentName.Trim();
        model.ParentOccupation = string.IsNullOrWhiteSpace(model.ParentOccupation) ? null : model.ParentOccupation.Trim();
        model.ParentNo = string.IsNullOrWhiteSpace(model.ParentNo) ? null : model.ParentNo.Trim();
        model.Address = string.IsNullOrWhiteSpace(model.Address) ? null : model.Address.Trim();
        model.EducationalQualification = string.IsNullOrWhiteSpace(model.EducationalQualification) ? null : model.EducationalQualification.Trim();
        model.CollegeName = string.IsNullOrWhiteSpace(model.CollegeName) ? null : model.CollegeName.Trim();
        model.Department = string.IsNullOrWhiteSpace(model.Department) ? null : model.Department.Trim();
        model.CareerConsultant = string.IsNullOrWhiteSpace(model.CareerConsultant) ? null : model.CareerConsultant.Trim();
        model.HowFoundSmec = string.IsNullOrWhiteSpace(model.HowFoundSmec) ? null : model.HowFoundSmec.Trim();
        model.BranchLocation = string.IsNullOrWhiteSpace(model.BranchLocation) ? null : model.BranchLocation.Trim();
        model.SkillSector = string.IsNullOrWhiteSpace(model.SkillSector) ? null : model.SkillSector.Trim();
        model.CourseWithFee = string.IsNullOrWhiteSpace(model.CourseWithFee) ? null : model.CourseWithFee.Trim();
        model.ApplicationFeeStatus = string.IsNullOrWhiteSpace(model.ApplicationFeeStatus) ? null : model.ApplicationFeeStatus.Trim();
        model.Password = string.IsNullOrWhiteSpace(model.Password) ? null : model.Password.Trim();
        model.ConfirmPassword = string.IsNullOrWhiteSpace(model.ConfirmPassword) ? null : model.ConfirmPassword.Trim();

        if (model.IsRegistrationMode)
        {
            model.AdmissionNumber = string.Empty;
            model.AdmissionFee = null;
            model.SectionId = null;
        }
    }

    private static string GenerateTemporaryPassword()
    {
        return $"RegAa1{Guid.NewGuid():N}"[..14];
    }

    private static string NormalizeDashboardRange(string? range)
    {
        var normalizedRange = range?.Trim().ToLowerInvariant();

        return normalizedRange switch
        {
            "thismonth" => "thisMonth",
            "thisyear" => "thisYear",
            _ => "thisWeek"
        };
    }

    private static (DateTime StartDate, DateTime EndDate, string RangeLabel) GetDashboardRange(string range, DateTime today)
    {
        return range switch
        {
            "thisMonth" => (new DateTime(today.Year, today.Month, 1), today, "This Month"),
            "thisYear" => (new DateTime(today.Year, 1, 1), today, "This Year"),
            _ => (GetStartOfWeek(today), today, "This Week")
        };
    }

    private static DateTime GetStartOfWeek(DateTime date)
    {
        var diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
        return date.AddDays(-diff).Date;
    }

    private static List<(string Label, int Value)> BuildDashboardChartPoints(
        string range,
        DateTime startDate,
        DateTime endDate,
        List<Attendance> presentAttendances)
    {
        if (range == "thisYear")
        {
            return Enumerable.Range(1, endDate.Month)
                .Select(month =>
                {
                    var label = new DateTime(endDate.Year, month, 1).ToString("MMM");
                    var value = presentAttendances.Count(a => a.Date.Year == endDate.Year && a.Date.Month == month);
                    return (label, value);
                })
                .ToList();
        }

        var totalDays = (endDate.Date - startDate.Date).Days + 1;

        return Enumerable.Range(0, totalDays)
            .Select(offset =>
            {
                var currentDate = startDate.AddDays(offset).Date;
                var label = range == "thisMonth"
                    ? currentDate.ToString("dd MMM")
                    : currentDate.ToString("ddd");
                var value = presentAttendances.Count(a => a.Date.Date == currentDate);
                return (label, value);
            })
            .ToList();
    }
}
