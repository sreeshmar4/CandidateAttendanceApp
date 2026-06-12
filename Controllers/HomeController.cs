using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CandidateAttendanceApp.Data;
using CandidateAttendanceApp.Models;
using CandidateAttendanceApp.ViewModels;

namespace CandidateAttendanceApp.Controllers;

public class HomeController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<IdentityUser> _userManager;

    public HomeController(ApplicationDbContext context,
                          UserManager<IdentityUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public IActionResult Index()
    {
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Register()
    {
        var model = new AnonymousRegisterViewModel();
        model.Sections = await _context.Sections.Where(s => s.IsActive).ToListAsync();
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Register(AnonymousRegisterViewModel model)
    {
        model.Name = model.Name?.Trim() ?? string.Empty;
        model.Email = model.Email?.Trim() ?? string.Empty;
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

        // Resolve course from SectionId
        Section? selectedSection = null;
        if (model.SectionId.HasValue)
        {
            selectedSection = await _context.Sections
                .FirstOrDefaultAsync(s => s.Id == model.SectionId.Value && s.IsActive);
            if (selectedSection != null)
            {
                model.CourseWithFee = selectedSection.Name;
                model.ApplicationFee = selectedSection.Fee;
            }
        }

        if (string.IsNullOrWhiteSpace(model.Name))
            ModelState.AddModelError(nameof(model.Name), "Name is required.");

        if (string.IsNullOrWhiteSpace(model.Email))
            ModelState.AddModelError(nameof(model.Email), "Email is required.");

        if (!string.IsNullOrWhiteSpace(model.Email))
        {
            var existingUser = await _userManager.FindByEmailAsync(model.Email);
            if (existingUser != null)
                ModelState.AddModelError(nameof(model.Email), "A user with this email already exists.");
        }

        if (!ModelState.IsValid)
        {
            model.Sections = await _context.Sections.Where(s => s.IsActive).ToListAsync();
            return View(model);
        }

        var password = $"RegAa1{Guid.NewGuid():N}"[..14];

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
                ModelState.AddModelError(string.Empty, error.Description);
            return View(model);
        }

        await _userManager.AddToRoleAsync(user, "User");

        var userProfile = new UserProfile
        {
            UserId = user.Id,
            Name = model.Name,
            AdmissionNumber = string.Empty,
            PhoneNumber = model.PhoneNumber,
            ParentName = model.ParentName,
            ParentOccupation = model.ParentOccupation,
            ParentNo = model.ParentNo,
            Address = model.Address,
            EducationalQualification = model.EducationalQualification,
            CollegeName = model.CollegeName,
            YearOfPassout = model.YearOfPassout,
            Department = model.Department,
            CareerConsultant = model.CareerConsultant,
            HowFoundSmec = model.HowFoundSmec,
            BranchLocation = model.BranchLocation,
            SkillSector = model.SkillSector,
            CourseWithFee = model.CourseWithFee,
            ApplicationFee = model.ApplicationFee,
            ApplicationFeeAmount = model.ApplicationFeeAmount,
            CourseFee = model.ApplicationFeeAmount,
            ApplicationFeeStatus = model.ApplicationFeeStatus,
            TentativeStartMonth = model.TentativeStartMonth,
            SectionId = model.SectionId
        };

        _context.UserProfiles.Add(userProfile);
        await _context.SaveChangesAsync();

        TempData["RegisterSuccess"] = "Registration submitted successfully!";
        return RedirectToAction(nameof(Register));
    }
}