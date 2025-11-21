//--------------------------Start Of File--------------------------//
using CMCS.Attributes;
using CMCS.Data;
using CMCS.Models;
using CMCS.Services;
using CMCS.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.EntityFrameworkCore;

namespace CMCS.Controllers
{
    [SessionAuthorize("HR")]
    public class HRController : Controller
    {
        private readonly CMCSContext _context;
        private readonly IReportService _reportService;
        private readonly ILogger<HRController> _logger;

        public HRController(
            CMCSContext context,
            IReportService reportService,
            ILogger<HRController> logger)
        {
            _context = context;
            _reportService = reportService;
            _logger = logger;
        }

        // GET: HR/Dashboard
        public async Task<IActionResult> Dashboard()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var users = await _context.Users
                .OrderBy(u => u.LastName)
                .ThenBy(u => u.FirstName)
                .ToListAsync();

            var viewModel = new HRDashboardViewModel
            {
                Users = users,
                TotalLecturers = users.Count(u => u.Role == "Lecturer"),
                TotalCoordinators = users.Count(u => u.Role == "Coordinator"),
                TotalManagers = users.Count(u => u.Role == "Manager"),
                TotalUsers = users.Count,
                TotalApprovedClaims = await _context.Claims.CountAsync(c => c.CurrentStatus == ClaimStatus.Approved),
                TotalPaymentAmount = await _context.Claims.Where(c => c.CurrentStatus == ClaimStatus.Approved).SumAsync(c => c.TotalAmount)
            };

            return View(viewModel);
        }

        // GET: HR/CreateUser
        public IActionResult CreateUser()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            return View(new CreateUserViewModel());
        }

        // POST: HR/CreateUser
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateUser(CreateUserViewModel model)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            if (ModelState.IsValid)
            {
                // Check if email already exists
                if (await _context.Users.AnyAsync(u => u.Email == model.Email))
                {
                    ModelState.AddModelError("Email", "This email is already registered.");
                    return View(model);
                }

                var user = new User
                {
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    Email = model.Email,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password),
                    Role = model.Role,
                    Phone = model.Phone,
                    Department = model.Department,
                    Faculty = model.Faculty,
                    Campus = model.Campus,
                    HourlyRate = model.HourlyRate,
                    IsActive = true,
                    CreatedAt = DateTime.Now
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"User {user.FullName} has been created successfully.";
                return RedirectToAction(nameof(Dashboard));
            }

            return View(model);
        }

        // GET: HR/EditUser/5
        public async Task<IActionResult> EditUser(int id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            var model = new EditUserViewModel
            {
                UserId = user.UserId,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                Role = user.Role,
                Phone = user.Phone,
                Department = user.Department,
                Faculty = user.Faculty,
                Campus = user.Campus,
                HourlyRate = user.HourlyRate,
                IsActive = user.IsActive
            };

            return View(model);
        }

        // POST: HR/EditUser/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUser(int id, EditUserViewModel model)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            if (id != model.UserId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                var user = await _context.Users.FindAsync(id);
                if (user == null)
                {
                    return NotFound();
                }

                // Check if new email conflicts with another user
                if (model.Email != user.Email &&
                    await _context.Users.AnyAsync(u => u.Email == model.Email && u.UserId != id))
                {
                    ModelState.AddModelError("Email", "This email is already in use by another user.");
                    return View(model);
                }

                user.FirstName = model.FirstName;
                user.LastName = model.LastName;
                user.Email = model.Email;
                user.Role = model.Role;
                user.Phone = model.Phone;
                user.Department = model.Department;
                user.Faculty = model.Faculty;
                user.Campus = model.Campus;
                user.HourlyRate = model.HourlyRate;
                user.IsActive = model.IsActive;
                user.UpdatedAt = DateTime.Now;

                // Update password if provided
                if (!string.IsNullOrEmpty(model.Password))
                {
                    user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password);
                }

                await _context.SaveChangesAsync();

                TempData["Success"] = $"User {user.FullName} has been updated successfully.";
                return RedirectToAction(nameof(Dashboard));
            }

            return View(model);
        }

        // GET: HR/ViewUser/5
        public async Task<IActionResult> ViewUser(int id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var user = await _context.Users
                .Include(u => u.Claims)
                .ThenInclude(c => c.Module)
                .FirstOrDefaultAsync(u => u.UserId == id);

            if (user == null)
            {
                return NotFound();
            }

            return View(user);
        }

        // GET: HR/Reports
        public async Task<IActionResult> Reports()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var approvedClaims = await _context.Claims
                .Include(c => c.User)
                .Include(c => c.Module)
                .Where(c => c.CurrentStatus == ClaimStatus.Approved)
                .OrderByDescending(c => c.LastModified)
                .ToListAsync();

            return View(approvedClaims);
        }

        // POST: HR/GenerateReport
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateReport(string reportType, DateTime? startDate, DateTime? endDate)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var pdfBytes = await _reportService.GenerateClaimsReport(reportType, startDate, endDate);
                var fileName = $"Claims_Report_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating report");
                TempData["Error"] = "An error occurred while generating the report.";
                return RedirectToAction(nameof(Reports));
            }
        }

        // GET: HR/GenerateAllClaimsReport
        public async Task<IActionResult> GenerateAllClaimsReport()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var pdfBytes = await _reportService.GenerateClaimsReport("all", null, null);
                var fileName = $"All_Claims_Report_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating all claims report");
                TempData["Error"] = "An error occurred while generating the report.";
                return RedirectToAction(nameof(Reports));
            }
        }

        // GET: HR/GenerateMonthlyReport
        public async Task<IActionResult> GenerateMonthlyReport()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var startDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                var endDate = startDate.AddMonths(1).AddDays(-1);

                var pdfBytes = await _reportService.GenerateClaimsReport("monthly", startDate, endDate);
                var fileName = $"Monthly_Report_{DateTime.Now:yyyyMM}.pdf";
                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating monthly report");
                TempData["Error"] = "An error occurred while generating the report.";
                return RedirectToAction(nameof(Reports));
            }
        }

        // GET: HR/Invoices
        public async Task<IActionResult> Invoices()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var lecturers = await _context.Users
                .Include(u => u.Claims)
                .Where(u => u.Role == "Lecturer")
                .OrderBy(u => u.LastName)
                .ThenBy(u => u.FirstName)
                .ToListAsync();

            return View(lecturers);
        }

        // POST: HR/GenerateInvoice
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateInvoice(int lecturerId, DateTime startDate, DateTime endDate)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var pdfBytes = await _reportService.GenerateInvoice(lecturerId, startDate, endDate);
                var lecturer = await _context.Users.FindAsync(lecturerId);
                var fileName = $"Invoice_{lecturer?.LastName}_{DateTime.Now:yyyyMMdd}.pdf";
                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating invoice for lecturer {LecturerId}", lecturerId);
                TempData["Error"] = "An error occurred while generating the invoice.";
                return RedirectToAction(nameof(Invoices));
            }
        }

        // GET: HR/QuickInvoice/5
        public async Task<IActionResult> QuickInvoice(int id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                // Generate invoice for all approved claims
                var startDate = DateTime.MinValue;
                var endDate = DateTime.Now;

                var pdfBytes = await _reportService.GenerateInvoice(id, startDate, endDate);
                var lecturer = await _context.Users.FindAsync(id);
                var fileName = $"Invoice_{lecturer?.LastName}_{DateTime.Now:yyyyMMdd}.pdf";
                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating quick invoice for lecturer {LecturerId}", id);
                TempData["Error"] = "An error occurred while generating the invoice.";
                return RedirectToAction(nameof(Invoices));
            }
        }
    }
}
//--------------------------End Of File--------------------------//