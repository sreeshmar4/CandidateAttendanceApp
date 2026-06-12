using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CandidateAttendanceApp.Data;

namespace CandidateAttendanceApp.Controllers;

public class AccountController : Controller
{
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly ApplicationDbContext _context;

    public AccountController(
        SignInManager<IdentityUser> signInManager,
        UserManager<IdentityUser> userManager,
        ApplicationDbContext context)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _context = context;
    }

    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Login(string email, string password, string? returnUrl = null)
    {
        var result = await _signInManager.PasswordSignInAsync(
            email, password, false, false);

        if (result.Succeeded)
        {
            var user = await _userManager.FindByEmailAsync(email);

            // Check if the user is soft-deleted
            var isDeleted = await _context.UserProfiles
                .AnyAsync(up => up.UserId == user.Id && up.IsDeleted);

            if (isDeleted)
            {
                await _signInManager.SignOutAsync();
                ViewBag.Error = "Your account has been deleted. Please contact the administrator.";
                return View();
            }

            if (await _userManager.IsInRoleAsync(user, "Admin"))
                return RedirectToAction("Dashboard", "Admin");

            return RedirectToAction("Dashboard", "Attendance");
        }

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            TempData["LoginError"] = "Invalid login attempt";
            return LocalRedirect(returnUrl);
        }

        ViewBag.Error = "Invalid login attempt";
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Login");
    }
}
