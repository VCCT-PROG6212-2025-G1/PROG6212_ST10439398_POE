//--------------------------Start Of File--------------------------//
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
        public IActionResult Login(string returnUrl = null)
        {
            // ? FIXED: Prevent redirect loop by checking if already on login page
            var currentPath = HttpContext.Request.Path.Value?.ToLower();
            if (currentPath == "/account/login")
            {
                // Already on login page, clear any stale session
                if (HttpContext.Session.GetInt32("UserId") != null)
                {
                    HttpContext.Session.Clear();
                }
                ViewData["ReturnUrl"] = returnUrl;
                return View();
            }

            // Check if user is already logged in
            var userId = HttpContext.Session.GetInt32("UserId");
            var userRole = HttpContext.Session.GetString("UserRole");

            if (userId != null && !string.IsNullOrEmpty(userRole))
            {
                // User is logged in, redirect to their dashboard
                return RedirectToDashboard();
            }

            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string returnUrl = null)
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

                // Verify password using BCrypt
                if (string.IsNullOrEmpty(user.PasswordHash) ||
                    !BCrypt.Net.BCrypt.Verify(model.Password, user.PasswordHash))
                {
                    ModelState.AddModelError("", "Invalid login credentials.");
                    TempData["Error"] = "Invalid password. Please try again.";
                    _logger.LogWarning("Failed login attempt for user: {Email}", model.Email);
                    return View(model);
                }

                // Clear any existing session data first
                HttpContext.Session.Clear();

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
                    IsPersistent = model.RememberMe,  // Use remember me checkbox
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8),
                    AllowRefresh = true
                };

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity),
                    authProperties);

                // Store user information in session for authorization checks
                HttpContext.Session.SetInt32("UserId", user.UserId);
                HttpContext.Session.SetString("UserRole", user.UserRole.ToString());
                HttpContext.Session.SetString("UserEmail", user.Email);
                HttpContext.Session.SetString("UserFullName", $"{user.FirstName} {user.LastName}");
                HttpContext.Session.SetString("HourlyRate", user.HourlyRate.ToString());

                _logger.LogInformation("User {UserId} logged in successfully. Session created with Role: {Role}",
                    user.UserId, user.UserRole);

                TempData["Success"] = $"Welcome back, {user.FirstName}!";

                // Handle return URL or redirect based on role
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return Redirect(returnUrl);
                }

                // Redirect based on role
                return user.UserRole switch
                {
                    UserRole.Lecturer => RedirectToAction("Dashboard", "Lecturer"),
                    UserRole.Coordinator => RedirectToAction("Dashboard", "Coordinator"),
                    UserRole.Manager => RedirectToAction("Dashboard", "Manager"),
                    UserRole.HR => RedirectToAction("Dashboard", "HR"),
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
                var userId = HttpContext.Session.GetInt32("UserId");

                // Sign out from cookie authentication
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

                // Clear session completely
                HttpContext.Session.Clear();

                // Clear response cache
                Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
                Response.Headers["Pragma"] = "no-cache";
                Response.Headers["Expires"] = "0";

                _logger.LogInformation("User {UserId} logged out. Session cleared.", userId);

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
            // Don't require auth for access denied page
            return View();
        }

        private IActionResult RedirectToDashboard()
        {
            var role = HttpContext.Session.GetString("UserRole");

            if (string.IsNullOrEmpty(role))
            {
                // Fallback to claims if session is empty
                if (User.Identity?.IsAuthenticated == true)
                {
                    var claimRole = User.FindFirst(ClaimTypes.Role)?.Value;
                    if (!string.IsNullOrEmpty(claimRole))
                    {
                        role = claimRole;
                    }
                }
            }

            return role switch
            {
                "Lecturer" => RedirectToAction("Dashboard", "Lecturer"),
                "Coordinator" => RedirectToAction("Dashboard", "Coordinator"),
                "Manager" => RedirectToAction("Dashboard", "Manager"),
                "HR" => RedirectToAction("Dashboard", "HR"),
                _ => RedirectToAction("Index", "Home")
            };
        }
    }
}
//--------------------------End Of File--------------------------//