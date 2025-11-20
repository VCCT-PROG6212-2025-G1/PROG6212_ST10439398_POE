//--------------------------Start Of File--------------------------//
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CMCS.Data;
using CMCS.Models;
using CMCS.Services;

namespace CMCS.Controllers.Api
{

    /// Micro API for HR automation
    /// Handles user management and report generation

    [Route("api/v1/hr")]
    [ApiController]
    public class HRApiController : ControllerBase
    {
        private readonly CMCSContext _context;
        private readonly ILogger<HRApiController> _logger;
        private readonly IReportService _reportService;

        public HRApiController(CMCSContext context, ILogger<HRApiController> logger, IReportService reportService)
        {
            _context = context;
            _logger = logger;
            _reportService = reportService;
        }

        #region User Management


        /// Get all users

        [HttpGet("users")]
        public async Task<ActionResult<IEnumerable<UserDto>>> GetUsers([FromQuery] string? role = null)
        {
            try
            {
                var query = _context.Users.AsQueryable();

                if (!string.IsNullOrEmpty(role) && Enum.TryParse<UserRole>(role, out var userRole))
                {
                    query = query.Where(u => u.UserRole == userRole);
                }

                var users = await query
                    .OrderBy(u => u.LastName)
                    .ThenBy(u => u.FirstName)
                    .Select(u => new UserDto
                    {
                        UserId = u.UserId,
                        FirstName = u.FirstName,
                        LastName = u.LastName,
                        Email = u.Email,
                        PhoneNumber = u.PhoneNumber,
                        Role = u.UserRole.ToString(),
                        HourlyRate = u.HourlyRate,
                        IsActive = u.IsActive,
                        CreatedDate = u.CreatedDate
                    })
                    .ToListAsync();

                return Ok(users);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving users");
                return StatusCode(500, new { error = "Error retrieving users" });
            }
        }

    
        /// Get a specific user
   
        [HttpGet("users/{id}")]
        public async Task<ActionResult<UserDto>> GetUser(int id)
        {
            try
            {
                var user = await _context.Users.FindAsync(id);

                if (user == null)
                {
                    return NotFound(new { error = $"User {id} not found" });
                }

                return Ok(new UserDto
                {
                    UserId = user.UserId,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Email = user.Email,
                    PhoneNumber = user.PhoneNumber,
                    Role = user.UserRole.ToString(),
                    HourlyRate = user.HourlyRate,
                    IsActive = user.IsActive,
                    CreatedDate = user.CreatedDate
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user {UserId}", id);
                return StatusCode(500, new { error = "Error retrieving user" });
            }
        }


        /// Create a new user (HR creates all accounts)
        /// POST /api/v1/hr/users

        [HttpPost("users")]
        public async Task<ActionResult<UserDto>> CreateUser([FromBody] CreateUserRequest request)
        {
            try
            {
                // Check if email already exists
                var existingUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email == request.Email);

                if (existingUser != null)
                {
                    return BadRequest(new { error = "A user with this email already exists" });
                }

                // Parse role
                if (!Enum.TryParse<UserRole>(request.Role, out var userRole))
                {
                    return BadRequest(new { error = "Invalid role specified" });
                }

                var user = new User
                {
                    FirstName = request.FirstName,
                    LastName = request.LastName,
                    Email = request.Email,
                    PhoneNumber = request.PhoneNumber,
                    UserRole = userRole,
                    HourlyRate = request.HourlyRate,
                    IsActive = true,
                    CreatedDate = DateTime.Now
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                _logger.LogInformation("User {UserId} created via API: {Email} as {Role}",
                    user.UserId, user.Email, user.UserRole);

                return CreatedAtAction(nameof(GetUser), new { id = user.UserId }, new UserDto
                {
                    UserId = user.UserId,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Email = user.Email,
                    PhoneNumber = user.PhoneNumber,
                    Role = user.UserRole.ToString(),
                    HourlyRate = user.HourlyRate,
                    IsActive = user.IsActive,
                    CreatedDate = user.CreatedDate
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user");
                return StatusCode(500, new { error = "Error creating user" });
            }
        }


        /// Update user information
        /// PUT /api/v1/hr/users/{id}
  
        [HttpPut("users/{id}")]
        public async Task<ActionResult<UserDto>> UpdateUser(int id, [FromBody] UpdateUserRequest request)
        {
            try
            {
                var user = await _context.Users.FindAsync(id);

                if (user == null)
                {
                    return NotFound(new { error = $"User {id} not found" });
                }

                // Check if email is being changed and if it's already in use
                if (request.Email != user.Email)
                {
                    var existingUser = await _context.Users
                        .FirstOrDefaultAsync(u => u.Email == request.Email && u.UserId != id);

                    if (existingUser != null)
                    {
                        return BadRequest(new { error = "Email is already in use by another user" });
                    }
                }

                user.FirstName = request.FirstName;
                user.LastName = request.LastName;
                user.Email = request.Email;
                user.PhoneNumber = request.PhoneNumber;
                user.HourlyRate = request.HourlyRate;
                user.IsActive = request.IsActive;
                user.LastModified = DateTime.Now;

                _context.Entry(user).State = EntityState.Modified;
                await _context.SaveChangesAsync();

                _logger.LogInformation("User {UserId} updated via API", id);

                return Ok(new UserDto
                {
                    UserId = user.UserId,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Email = user.Email,
                    PhoneNumber = user.PhoneNumber,
                    Role = user.UserRole.ToString(),
                    HourlyRate = user.HourlyRate,
                    IsActive = user.IsActive,
                    CreatedDate = user.CreatedDate
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user {UserId}", id);
                return StatusCode(500, new { error = "Error updating user" });
            }
        }

 
        /// Toggle user active status
   
        [HttpPost("users/{id}/toggle-active")]
        public async Task<ActionResult> ToggleUserActive(int id)
        {
            try
            {
                var user = await _context.Users.FindAsync(id);

                if (user == null)
                {
                    return NotFound(new { error = $"User {id} not found" });
                }

                user.IsActive = !user.IsActive;
                user.LastModified = DateTime.Now;

                await _context.SaveChangesAsync();

                _logger.LogInformation("User {UserId} active status toggled to {Status}", id, user.IsActive);

                return Ok(new { message = $"User {(user.IsActive ? "activated" : "deactivated")} successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling user {UserId} status", id);
                return StatusCode(500, new { error = "Error updating user status" });
            }
        }

        #endregion

        #region Reports

 
        /// Generate approved claims report
        /// GET /api/v1/hr/reports/claims

        [HttpGet("reports/claims")]
        public async Task<IActionResult> GetApprovedClaimsReport(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
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
                return StatusCode(500, new { error = "Error generating report" });
            }
        }


        /// Generate invoice for a specific claim
        /// GET /api/v1/hr/reports/invoice/{claimId}

        [HttpGet("reports/invoice/{claimId}")]
        public async Task<IActionResult> GetInvoice(int claimId)
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
                return StatusCode(500, new { error = "Error generating invoice" });
            }
        }


        /// Generate lecturer report

        [HttpGet("reports/lecturer/{lecturerId}")]
        public async Task<IActionResult> GetLecturerReport(
            int lecturerId,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
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
                return StatusCode(500, new { error = "Error generating report" });
            }
        }


        /// Generate monthly summary report

        [HttpGet("reports/monthly")]
        public async Task<IActionResult> GetMonthlyReport(
            [FromQuery] int year,
            [FromQuery] int month)
        {
            try
            {
                if (month < 1 || month > 12)
                {
                    return BadRequest(new { error = "Month must be between 1 and 12" });
                }

                var reportBytes = await _reportService.GenerateMonthlyReportAsync(year, month);

                var fileName = $"MonthlyReport_{year}-{month:D2}.html";

                return File(reportBytes, "text/html", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating monthly report for {Year}-{Month}", year, month);
                return StatusCode(500, new { error = "Error generating report" });
            }
        }

        /// Get dashboard statistics for HR

        [HttpGet("stats")]
        public async Task<ActionResult<HRStatsDto>> GetStats()
        {
            try
            {
                var today = DateTime.Today;
                var thisMonth = new DateTime(today.Year, today.Month, 1);

                var stats = new HRStatsDto
                {
                    TotalUsers = await _context.Users.CountAsync(),
                    TotalLecturers = await _context.Users.CountAsync(u => u.UserRole == UserRole.Lecturer),
                    ActiveUsers = await _context.Users.CountAsync(u => u.IsActive),
                    TotalClaims = await _context.Claims.CountAsync(),
                    PendingClaims = await _context.Claims.CountAsync(c =>
                        c.CurrentStatus == ClaimStatus.Submitted || c.CurrentStatus == ClaimStatus.UnderReview),
                    ApprovedClaims = await _context.Claims.CountAsync(c => c.CurrentStatus == ClaimStatus.Approved),
                    TotalApprovedAmount = await _context.Claims
                        .Where(c => c.CurrentStatus == ClaimStatus.Approved)
                        .SumAsync(c => (decimal?)c.TotalAmount) ?? 0,
                    ClaimsThisMonth = await _context.Claims
                        .CountAsync(c => c.SubmissionDate >= thisMonth)
                };

                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving HR stats");
                return StatusCode(500, new { error = "Error retrieving statistics" });
            }
        }

        #endregion
    }

    #region DTOs

    public class UserDto
    {
        public int UserId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string Role { get; set; }
        public decimal HourlyRate { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public class CreateUserRequest
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string Role { get; set; }
        public decimal HourlyRate { get; set; }
    }

    public class UpdateUserRequest
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public decimal HourlyRate { get; set; }
        public bool IsActive { get; set; }
    }

    public class HRStatsDto
    {
        public int TotalUsers { get; set; }
        public int TotalLecturers { get; set; }
        public int ActiveUsers { get; set; }
        public int TotalClaims { get; set; }
        public int PendingClaims { get; set; }
        public int ApprovedClaims { get; set; }
        public decimal TotalApprovedAmount { get; set; }
        public int ClaimsThisMonth { get; set; }
    }

    #endregion
}
//--------------------------End Of File--------------------------//