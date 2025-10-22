using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CMCS.Data;
using System.Security.Claims;

namespace CMCS.Controllers
{
    [Authorize]
    public class AccountSettingsController : Controller
    {
        private readonly CMCSContext _context;
        private readonly ILogger<AccountSettingsController> _logger;

        public AccountSettingsController(CMCSContext context, ILogger<AccountSettingsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var user = await _context.Users.FindAsync(userId);

                if (user == null)
                {
                    TempData["Error"] = "User not found.";
                    return RedirectToAction("Dashboard", GetDashboardController());
                }

                return View(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading account settings");
                TempData["Error"] = "Error loading account settings.";
                return RedirectToAction("Dashboard", GetDashboardController());
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(CMCS.Models.User model)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var user = await _context.Users.FindAsync(userId);

                if (user == null)
                {
                    TempData["Error"] = "User not found.";
                    return RedirectToAction(nameof(Index));
                }

                // Update only allowed fields
                user.FirstName = model.FirstName;
                user.LastName = model.LastName;
                user.PhoneNumber = model.PhoneNumber;

                await _context.SaveChangesAsync();
                TempData["Success"] = "Profile updated successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating profile");
                TempData["Error"] = "Error updating profile.";
                return RedirectToAction(nameof(Index));
            }
        }

        private string GetDashboardController()
        {
            if (User.IsInRole("Lecturer")) return "Lecturer";
            if (User.IsInRole("Coordinator")) return "Coordinator";
            if (User.IsInRole("Manager")) return "Manager";
            return "Home";
        }
    }
}