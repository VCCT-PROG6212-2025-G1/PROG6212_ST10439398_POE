//--------------------------Start Of File--------------------------//
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CMCS.Data;
using CMCS.Models;
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
        public async Task<IActionResult> UpdateProfile(User model)
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

                // Only update allowed fields
                user.FirstName = model.FirstName;
                user.LastName = model.LastName;
                user.PhoneNumber = model.PhoneNumber;

                // Mark entity as modified
                _context.Entry(user).State = EntityState.Modified;

                await _context.SaveChangesAsync();

                TempData["Success"] = "Profile updated successfully!";
                _logger.LogInformation("User {UserId} updated profile", userId);

                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error updating profile for user {UserId}", User.FindFirstValue(ClaimTypes.NameIdentifier));
                TempData["Error"] = "Database error updating profile. Please try again.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating profile");
                TempData["Error"] = "Error updating profile. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        private string GetDashboardController()
        {
            if (User.IsInRole("Lecturer")) return "Lecturer";
            if (User.IsInRole("Coordinator")) return "Coordinator";
            if (User.IsInRole("Manager")) return "Coordinator";
            return "Home";
        }
    }
}
//--------------------------End Of File--------------------------//