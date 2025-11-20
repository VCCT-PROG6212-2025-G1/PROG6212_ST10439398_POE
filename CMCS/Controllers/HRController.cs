//--------------------------Start Of File--------------------------//
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using CMCS.Models;
using CMCS.Data;
using CMCS.Services;
using CMCS.Attributes;

namespace CMCS.Controllers
{
    [Authorize(Roles = "HR")]
    public class HRController : Controller
    {
        private readonly CMCSContext _context;
        private readonly ILogger<HRController> _logger;
        private readonly IReportService _reportService;

        public HRController(CMCSContext context, ILogger<HRController> logger, IReportService reportService)
        {
            _context = context;
            _logger = logger;
            _reportService = reportService;
        }

        public async Task<IActionResult> Dashboard()
        {
            // Session check - MANDATORY for Part 3
            var userId = HttpContext.Session.GetInt32("UserId");
            var userRole = HttpContext.Session.GetString("UserRole");

            if (userId == null || userRole != "HR")
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var today = DateTime.Today;
                var thisMonth = new DateTime(today.Year, today.Month, 1);

                var viewModel = new HRDashboardViewModel
                {
                    TotalUsers = await _context.Users.CountAsync(),
                    TotalLecturers = await _context.Users.CountAsync(u => u.UserRole == UserRole.Lecturer),
                    PendingClaims = await _context.Claims
                        .CountAsync(c => c.CurrentStatus == ClaimStatus.Submitted ||
                                        c.CurrentStatus == ClaimStatus.UnderReview),
                    ApprovedClaimsThisMonth = await _context.Claims
                        .CountAsync(c => c.CurrentStatus == ClaimStatus.Approved &&
                                        c.SubmissionDate >= thisMonth),
                    TotalApprovedAmount = await _context.Claims
                        .Where(c => c.CurrentStatus == ClaimStatus.Approved)
                        .SumAsync(c => (decimal?)c.TotalAmount) ?? 0,

                    RecentUsers = await _context.Users
                        .OrderByDescending(u => u.CreatedDate)
                        .Take(5)
                        .ToListAsync(),

                    ApprovedClaims = await _context.Claims
                        .Include(c => c.User)
                        .Include(c => c.Module)
                        .Where(c => c.CurrentStatus == ClaimStatus.Approved)
                        .OrderByDescending(c => c.LastModified)
                        .Take(10)
                        .ToListAsync()
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading HR dashboard");
                TempData["Error"] = "An error occurred loading the dashboard.";
                return View(new HRDashboardViewModel());
            }
        }

        #region User Management

        public async Task<IActionResult> Users(string? role = null, string? search = null)
        {
            // Session check
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var query = _context.Users.AsQueryable();

                if (!string.IsNullOrEmpty(role) && Enum.TryParse<UserRole>(role, out var userRole))
                {
                    query = query.Where(u => u.UserRole == userRole);
                }

                if (!string.IsNullOrEmpty(search))
                {
                    query = query.Where(u =>
                        u.FirstName.Contains(search) ||
                        u.LastName.Contains(search) ||
                        u.Email.Contains(search));
                }

                var users = await query
                    .OrderBy(u => u.UserRole)
                    .ThenBy(u => u.LastName)
                    .ToListAsync();

                ViewBag.CurrentRole = role;
                ViewBag.CurrentSearch = search;

                return View(users);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading users list");
                TempData["Error"] = "An error occurred loading the users.";
                return View(new List<User>());
            }
        }

        public IActionResult CreateUser()
        {
            // Session check
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            return View(new CreateUserViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateUser(CreateUserViewModel model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return View(model);
                }

                // Check if email already exists
                var existingUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email == model.Email);

                if (existingUser != null)
                {
                    ModelState.AddModelError("Email", "A user with this email already exists.");
                    return View(model);
                }

                var user = new User
                {
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    Email = model.Email,
                    PhoneNumber = model.PhoneNumber,
                    UserRole = model.UserRole,
                    HourlyRate = model.HourlyRate,
                    IsActive = true,
                    CreatedDate = DateTime.Now
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                _logger.LogInformation("HR created new user {UserId}: {Email} as {Role}",
                    user.UserId, user.Email, user.UserRole);

                TempData["Success"] = $"User {user.FirstName} {user.LastName} created successfully!";
                return RedirectToAction(nameof(Users));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user");
                TempData["Error"] = "An error occurred creating the user.";
                return View(model);
            }
        }

        public async Task<IActionResult> EditUser(int id)
        {
            // Session check
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var user = await _context.Users.FindAsync(id);

                if (user == null)
                {
                    TempData["Error"] = "User not found.";
                    return RedirectToAction(nameof(Users));
                }

                var viewModel = new EditUserViewModel
                {
                    UserId = user.UserId,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Email = user.Email,
                    PhoneNumber = user.PhoneNumber,
                    UserRole = user.UserRole,
                    HourlyRate = user.HourlyRate,
                    IsActive = user.IsActive
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading user {UserId} for edit", id);
                TempData["Error"] = "An error occurred loading the user.";
                return RedirectToAction(nameof(Users));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUser(EditUserViewModel model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return View(model);
                }

                var user = await _context.Users.FindAsync(model.UserId);

                if (user == null)
                {
                    TempData["Error"] = "User not found.";
                    return RedirectToAction(nameof(Users));
                }

                // Check if email is being changed and if it's already in use
                if (model.Email != user.Email)
                {
                    var existingUser = await _context.Users
                        .FirstOrDefaultAsync(u => u.Email == model.Email && u.UserId != model.UserId);

                    if (existingUser != null)
                    {
                        ModelState.AddModelError("Email", "Email is already in use by another user.");
                        return View(model);
                    }
                }

                user.FirstName = model.FirstName;
                user.LastName = model.LastName;
                user.Email = model.Email;
                user.PhoneNumber = model.PhoneNumber;
                user.HourlyRate = model.HourlyRate;
                user.IsActive = model.IsActive;
                user.LastModified = DateTime.Now;

                _context.Entry(user).State = EntityState.Modified;
                await _context.SaveChangesAsync();

                _logger.LogInformation("HR updated user {UserId}", model.UserId);

                TempData["Success"] = $"User {user.FirstName} {user.LastName} updated successfully!";
                return RedirectToAction(nameof(Users));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user {UserId}", model.UserId);
                TempData["Error"] = "An error occurred updating the user.";
                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleUserActive(int id)
        {
            try
            {
                var user = await _context.Users.FindAsync(id);

                if (user == null)
                {
                    TempData["Error"] = "User not found.";
                    return RedirectToAction(nameof(Users));
                }

                user.IsActive = !user.IsActive;
                user.LastModified = DateTime.Now;

                await _context.SaveChangesAsync();

                _logger.LogInformation("HR toggled user {UserId} active status to {Status}", id, user.IsActive);

                TempData["Success"] = $"User {user.FirstName} {user.LastName} {(user.IsActive ? "activated" : "deactivated")} successfully!";
                return RedirectToAction(nameof(Users));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling user {UserId} status", id);
                TempData["Error"] = "An error occurred updating the user status.";
                return RedirectToAction(nameof(Users));
            }
        }

        #endregion

        #region Reports

        public async Task<IActionResult> Reports()
        {
            // Session check
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var viewModel = new ReportsViewModel
            {
                Lecturers = await _context.Users
                    .Where(u => u.UserRole == UserRole.Lecturer)
                    .OrderBy(u => u.LastName)
                    .ToListAsync(),

                ApprovedClaims = await _context.Claims
                    .Include(c => c.User)
                    .Include(c => c.Module)
                    .Where(c => c.CurrentStatus == ClaimStatus.Approved)
                    .OrderByDescending(c => c.LastModified)
                    .ToListAsync()
            };

            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> GenerateApprovedClaimsReport(DateTime? startDate, DateTime? endDate)
        {
            try
            {
                var reportBytes = await _reportService.GenerateApprovedClaimsReportAsync(startDate, endDate);
                var fileName = $"ApprovedClaims_{DateTime.Now:yyyyMMdd_HHmmss}.html";

                return File(reportBytes, "text/html", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating approved claims report");
                TempData["Error"] = "An error occurred generating the report.";
                return RedirectToAction(nameof(Reports));
            }
        }

        [HttpGet]
        public async Task<IActionResult> GenerateInvoice(int claimId)
        {
            try
            {
                var reportBytes = await _reportService.GenerateInvoiceAsync(claimId);
                var fileName = $"Invoice_CLC-{claimId:D4}_{DateTime.Now:yyyyMMdd}.html";

                return File(reportBytes, "text/html", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating invoice for claim {ClaimId}", claimId);
                TempData["Error"] = "An error occurred generating the invoice.";
                return RedirectToAction(nameof(Reports));
            }
        }

        [HttpGet]
        public async Task<IActionResult> GenerateLecturerReport(int lecturerId, DateTime? startDate, DateTime? endDate)
        {
            try
            {
                var reportBytes = await _reportService.GenerateLecturerReportAsync(lecturerId, startDate, endDate);
                var fileName = $"LecturerReport_{lecturerId}_{DateTime.Now:yyyyMMdd}.html";

                return File(reportBytes, "text/html", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating lecturer report for {LecturerId}", lecturerId);
                TempData["Error"] = "An error occurred generating the report.";
                return RedirectToAction(nameof(Reports));
            }
        }

        [HttpGet]
        public async Task<IActionResult> GenerateMonthlyReport(int year, int month)
        {
            try
            {
                var reportBytes = await _reportService.GenerateMonthlyReportAsync(year, month);
                var fileName = $"MonthlyReport_{year}-{month:D2}.html";

                return File(reportBytes, "text/html", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating monthly report for {Year}-{Month}", year, month);
                TempData["Error"] = "An error occurred generating the report.";
                return RedirectToAction(nameof(Reports));
            }
        }

        #endregion

        #region Claims Management

        public async Task<IActionResult> Claims(string? status = null)
        {
            // Session check
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var query = _context.Claims
                    .Include(c => c.User)
                    .Include(c => c.Module)
                    .AsQueryable();

                if (!string.IsNullOrEmpty(status) && Enum.TryParse<ClaimStatus>(status, out var claimStatus))
                {
                    query = query.Where(c => c.CurrentStatus == claimStatus);
                }

                var claims = await query
                    .OrderByDescending(c => c.SubmissionDate)
                    .ToListAsync();

                ViewBag.CurrentStatus = status;

                return View(claims);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading claims for HR");
                TempData["Error"] = "An error occurred loading the claims.";
                return View(new List<Claim>());
            }
        }

        #endregion
    }

    #region ViewModels

    public class HRDashboardViewModel
    {
        public int TotalUsers { get; set; }
        public int TotalLecturers { get; set; }
        public int PendingClaims { get; set; }
        public int ApprovedClaimsThisMonth { get; set; }
        public decimal TotalApprovedAmount { get; set; }
        public List<User> RecentUsers { get; set; } = new();
        public List<Claim> ApprovedClaims { get; set; } = new();
    }

    public class CreateUserViewModel
    {
        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.StringLength(100)]
        public string FirstName { get; set; }

        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.StringLength(100)]
        public string LastName { get; set; }

        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.EmailAddress]
        public string Email { get; set; }

        [System.ComponentModel.DataAnnotations.Phone]
        public string PhoneNumber { get; set; }

        [System.ComponentModel.DataAnnotations.Required]
        public UserRole UserRole { get; set; }

        [System.ComponentModel.DataAnnotations.Range(0, 10000)]
        public decimal HourlyRate { get; set; }
    }

    public class EditUserViewModel
    {
        public int UserId { get; set; }

        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.StringLength(100)]
        public string FirstName { get; set; }

        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.StringLength(100)]
        public string LastName { get; set; }

        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.EmailAddress]
        public string Email { get; set; }

        [System.ComponentModel.DataAnnotations.Phone]
        public string PhoneNumber { get; set; }

        public UserRole UserRole { get; set; }

        [System.ComponentModel.DataAnnotations.Range(0, 10000)]
        public decimal HourlyRate { get; set; }

        public bool IsActive { get; set; }
    }

    public class ReportsViewModel
    {
        public List<User> Lecturers { get; set; } = new();
        public List<Claim> ApprovedClaims { get; set; } = new();
    }

    #endregion
}
//--------------------------End Of File--------------------------//