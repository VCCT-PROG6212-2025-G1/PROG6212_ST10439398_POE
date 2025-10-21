using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using CMCS.Models;
using CMCS.ViewModels;
using CMCS.Data;

namespace CMCS.Controllers
{
    public class AccountController : Controller
    {
        private readonly CMCSContext _context;
        private readonly ILogger<AccountController> _logger;

        public AccountController(CMCSContext context, ILogger<AccountController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Login()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToDashboard();
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return View(model);
                }

                // Parse the selected role
                if (!Enum.TryParse<UserRole>(model.UserType, out var selectedRole))
                {
                    ModelState.AddModelError("", "Invalid role selected.");
                    return View(model);
                }

                // Find user by email, active status, and role
                var user = await _context.Users
                    .FirstOrDefaultAsync(u =>
                        u.Email == model.Email &&
                        u.IsActive &&
                        u.UserRole == selectedRole);

                if (user == null)
                {
                    ModelState.AddModelError("", "Invalid login credentials or user not found.");
                    TempData["Error"] = "Invalid email or role. Please check your credentials.";
                    return View(model);
                }

                // Create authentication claims
                var claims = new List<System.Security.Claims.Claim>
                {
                    new System.Security.Claims.Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                    new System.Security.Claims.Claim(ClaimTypes.Name, user.Email ?? string.Empty),
                    new System.Security.Claims.Claim(ClaimTypes.GivenName, user.FirstName ?? string.Empty),
                    new System.Security.Claims.Claim(ClaimTypes.Surname, user.LastName ?? string.Empty),
                    new System.Security.Claims.Claim(ClaimTypes.Role, user.UserRole.ToString())
                };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var authProperties = new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(60)
                };

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity),
                    authProperties);

                TempData["Success"] = $"Welcome back, {user.FirstName}!";

                // Redirect based on role
                return user.UserRole switch
                {
                    UserRole.Lecturer => RedirectToAction("Dashboard", "Lecturer"),
                    UserRole.Coordinator => RedirectToAction("Dashboard", "Coordinator"),
                    UserRole.Manager => RedirectToAction("Dashboard", "Coordinator"),
                    UserRole.HR => RedirectToAction("Dashboard", "Home"),
                    _ => RedirectToAction("Index", "Home")
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login for email: {Email}", model.Email);
                ModelState.AddModelError("", "An error occurred during login. Please try again.");
                TempData["Error"] = "An error occurred. Please try again.";
                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            try
            {
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                TempData["Success"] = "You have been logged out successfully.";
                return RedirectToAction(nameof(Login));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during logout");
                TempData["Error"] = "An error occurred during logout.";
                return RedirectToAction(nameof(Login));
            }
        }

        public IActionResult AccessDenied()
        {
            return View();
        }

        private IActionResult RedirectToDashboard()
        {
            if (User.IsInRole("Lecturer"))
                return RedirectToAction("Dashboard", "Lecturer");
            if (User.IsInRole("Coordinator") || User.IsInRole("Manager"))
                return RedirectToAction("Dashboard", "Coordinator");
            if (User.IsInRole("HR"))
                return RedirectToAction("Dashboard", "Home");

            return RedirectToAction("Index", "Home");
        }
    }
}