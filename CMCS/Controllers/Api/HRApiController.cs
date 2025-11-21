//--------------------------Start Of File--------------------------//
using CMCS.Data;
using CMCS.Models;
using CMCS.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.EntityFrameworkCore;

namespace CMCS.Controllers.Api
{
    [Route("api/v1/[controller]")]
    [ApiController]
    public class HRApiController : ControllerBase
    {
        private readonly CMCSContext _context;
        private readonly IReportService _reportService;
        private readonly ILogger<HRApiController> _logger;

        public HRApiController(
            CMCSContext context,
            IReportService reportService,
            ILogger<HRApiController> logger)
        {
            _context = context;
            _reportService = reportService;
            _logger = logger;
        }

        // POST: api/v1/hr/users
        [HttpPost("users")]
        public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
        {
            try
            {
                // Check if email already exists
                if (await _context.Users.AnyAsync(u => u.Email == request.Email))
                {
                    return BadRequest(new { success = false, message = "Email already exists" });
                }

                var user = new User
                {
                    FirstName = request.FirstName,
                    LastName = request.LastName,
                    Email = request.Email,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                    Role = request.Role,
                    Phone = request.Phone,
                    Department = request.Department,
                    Faculty = request.Faculty,
                    Campus = request.Campus,
                    HourlyRate = request.HourlyRate,
                    IsActive = true,
                    CreatedAt = DateTime.Now
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "User created successfully",
                    userId = user.UserId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user");
                return StatusCode(500, new { success = false, message = "An error occurred" });
            }
        }

        // PUT: api/v1/hr/users/{id}
        [HttpPut("users/{id}")]
        public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateUserRequest request)
        {
            try
            {
                var user = await _context.Users.FindAsync(id);
                if (user == null)
                {
                    return NotFound(new { success = false, message = "User not found" });
                }

                // Check if new email conflicts with another user
                if (request.Email != user.Email &&
                    await _context.Users.AnyAsync(u => u.Email == request.Email && u.UserId != id))
                {
                    return BadRequest(new { success = false, message = "Email already in use by another user" });
                }

                user.FirstName = request.FirstName;
                user.LastName = request.LastName;
                user.Email = request.Email;
                user.Role = request.Role;
                user.Phone = request.Phone;
                user.Department = request.Department;
                user.Faculty = request.Faculty;
                user.Campus = request.Campus;
                user.HourlyRate = request.HourlyRate;
                user.IsActive = request.IsActive;
                user.UpdatedAt = DateTime.Now;

                // Update password if provided
                if (!string.IsNullOrEmpty(request.Password))
                {
                    user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
                }

                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "User updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user {UserId}", id);
                return StatusCode(500, new { success = false, message = "An error occurred" });
            }
        }

        // GET: api/v1/hr/users
        [HttpGet("users")]
        public async Task<IActionResult> GetAllUsers()
        {
            try
            {
                var users = await _context.Users
                    .OrderBy(u => u.LastName)
                    .ThenBy(u => u.FirstName)
                    .Select(u => new
                    {
                        u.UserId,
                        u.FirstName,
                        u.LastName,
                        FullName = u.FirstName + " " + u.LastName,
                        u.Email,
                        u.Role,
                        u.Phone,
                        u.Department,
                        u.Faculty,
                        u.Campus,
                        u.HourlyRate,
                        u.IsActive,
                        u.CreatedAt
                    })
                    .ToListAsync();

                return Ok(new { success = true, users });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting users");
                return StatusCode(500, new { success = false, message = "An error occurred" });
            }
        }

        // GET: api/v1/hr/users/{id}
        [HttpGet("users/{id}")]
        public async Task<IActionResult> GetUser(int id)
        {
            try
            {
                var user = await _context.Users
                    .Include(u => u.Claims)
                    .FirstOrDefaultAsync(u => u.UserId == id);

                if (user == null)
                {
                    return NotFound(new { success = false, message = "User not found" });
                }

                return Ok(new
                {
                    success = true,
                    user = new
                    {
                        user.UserId,
                        user.FirstName,
                        user.LastName,
                        FullName = user.FullName,
                        user.Email,
                        user.Role,
                        user.Phone,
                        user.Department,
                        user.Faculty,
                        user.Campus,
                        user.HourlyRate,
                        user.IsActive,
                        user.CreatedAt,
                        user.UpdatedAt,
                        TotalClaims = user.Claims.Count,
                        ApprovedClaims = user.Claims.Count(c => c.CurrentStatus == ClaimStatus.Approved),
                        TotalEarnings = user.Claims.Where(c => c.CurrentStatus == ClaimStatus.Approved).Sum(c => c.TotalAmount)
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user {UserId}", id);
                return StatusCode(500, new { success = false, message = "An error occurred" });
            }
        }

        // GET: api/v1/hr/reports/claims
        [HttpGet("reports/claims")]
        public async Task<IActionResult> GetClaimsReport([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
        {
            try
            {
                var query = _context.Claims
                    .Include(c => c.User)
                    .Include(c => c.Module)
                    .Where(c => c.CurrentStatus == ClaimStatus.Approved);

                if (startDate.HasValue)
                {
                    query = query.Where(c => c.SubmissionDate >= startDate.Value);
                }

                if (endDate.HasValue)
                {
                    query = query.Where(c => c.SubmissionDate <= endDate.Value);
                }

                var claims = await query
                    .OrderByDescending(c => c.SubmissionDate)
                    .Select(c => new
                    {
                        c.ClaimId,
                        LecturerName = c.User.FirstName + " " + c.User.LastName,
                        c.User.Email,
                        ModuleName = c.Module != null ? c.Module.ModuleName : "N/A",
                        c.HoursWorked,
                        c.HourlyRate,
                        c.TotalAmount,
                        c.ClaimPeriod,
                        c.SubmissionDate
                    })
                    .ToListAsync();

                var summary = new
                {
                    TotalClaims = claims.Count,
                    TotalHours = claims.Sum(c => c.HoursWorked),
                    TotalAmount = claims.Sum(c => c.TotalAmount),
                    UniqueLecturers = claims.Select(c => c.Email).Distinct().Count()
                };

                return Ok(new { success = true, claims, summary });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating claims report");
                return StatusCode(500, new { success = false, message = "An error occurred" });
            }
        }

        // GET: api/v1/hr/reports/invoices/{lecturerId}
        [HttpGet("reports/invoices/{lecturerId}")]
        public async Task<IActionResult> GenerateInvoice(int lecturerId, [FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
        {
            try
            {
                var user = await _context.Users
                    .Include(u => u.Claims.Where(c => c.CurrentStatus == ClaimStatus.Approved))
                    .ThenInclude(c => c.Module)
                    .FirstOrDefaultAsync(u => u.UserId == lecturerId);

                if (user == null)
                {
                    return NotFound(new { success = false, message = "User not found" });
                }

                var claims = user.Claims.AsQueryable();

                if (startDate.HasValue)
                {
                    claims = claims.Where(c => c.SubmissionDate >= startDate.Value);
                }

                if (endDate.HasValue)
                {
                    claims = claims.Where(c => c.SubmissionDate <= endDate.Value);
                }

                var claimsList = claims.ToList();

                var invoice = new
                {
                    InvoiceNumber = $"INV-{DateTime.Now:yyyyMMdd}-{lecturerId}",
                    GeneratedDate = DateTime.Now,
                    Lecturer = new
                    {
                        user.UserId,
                        user.FullName,
                        user.Email,
                        user.Department
                    },
                    Claims = claimsList.Select(c => new
                    {
                        c.ClaimId,
                        ModuleName = c.Module?.ModuleName ?? "N/A",
                        c.HoursWorked,
                        c.HourlyRate,
                        c.TotalAmount,
                        c.ClaimPeriod
                    }),
                    Summary = new
                    {
                        TotalClaims = claimsList.Count,
                        TotalHours = claimsList.Sum(c => c.HoursWorked),
                        TotalAmount = claimsList.Sum(c => c.TotalAmount)
                    }
                };

                return Ok(new { success = true, invoice });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating invoice for lecturer {LecturerId}", lecturerId);
                return StatusCode(500, new { success = false, message = "An error occurred" });
            }
        }

        // GET: api/v1/hr/dashboard
        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboardStats()
        {
            try
            {
                var stats = new
                {
                    TotalUsers = await _context.Users.CountAsync(),
                    TotalLecturers = await _context.Users.CountAsync(u => u.Role == "Lecturer"),
                    TotalCoordinators = await _context.Users.CountAsync(u => u.Role == "Coordinator"),
                    TotalManagers = await _context.Users.CountAsync(u => u.Role == "Manager"),
                    TotalClaims = await _context.Claims.CountAsync(),
                    PendingClaims = await _context.Claims.CountAsync(c => c.CurrentStatus == ClaimStatus.Submitted),
                    ApprovedClaims = await _context.Claims.CountAsync(c => c.CurrentStatus == ClaimStatus.Approved),
                    TotalPayments = await _context.Claims.Where(c => c.CurrentStatus == ClaimStatus.Approved).SumAsync(c => c.TotalAmount)
                };

                return Ok(new { success = true, stats });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting dashboard stats");
                return StatusCode(500, new { success = false, message = "An error occurred" });
            }
        }
    }

    public class CreateUserRequest
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Department { get; set; }
        public string? Faculty { get; set; }
        public string? Campus { get; set; }
        public decimal HourlyRate { get; set; }
    }

    public class UpdateUserRequest
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Password { get; set; }
        public string Role { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Department { get; set; }
        public string? Faculty { get; set; }
        public string? Campus { get; set; }
        public decimal HourlyRate { get; set; }
        public bool IsActive { get; set; }
    }
}
//--------------------------End Of File--------------------------//