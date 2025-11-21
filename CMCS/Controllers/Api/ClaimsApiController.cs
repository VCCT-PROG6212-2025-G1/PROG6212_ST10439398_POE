//--------------------------Start Of File--------------------------//
using CMCS.Data;
using CMCS.Models;
using CMCS.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;


namespace CMCS.Controllers.Api
{
    [Route("api/v1/[controller]")]
    [ApiController]
    public class ClaimsApiController : ControllerBase
    {
        private readonly CMCSContext _context;
        private readonly IFileEncryptionService _encryptionService;
        private readonly ILogger<ClaimsApiController> _logger;

        public ClaimsApiController(
            CMCSContext context,
            IFileEncryptionService encryptionService,
            ILogger<ClaimsApiController> logger)
        {
            _context = context;
            _encryptionService = encryptionService;
            _logger = logger;
        }

        // POST: api/v1/claims/submit
        [HttpPost("submit")]
        public async Task<IActionResult> SubmitClaim([FromForm] ClaimSubmitRequest request)
        {
            try
            {
                // Validate user exists and is a lecturer
                var user = await _context.Users.FindAsync(request.UserId);
                if (user == null || user.UserRole != UserRole.Lecturer)
                {
                    return BadRequest(new { success = false, message = "Invalid user or user is not a lecturer" });
                }

                // Validate hours (max 180 per month)
                if (request.HoursWorked <= 0 || request.HoursWorked > 180)
                {
                    return BadRequest(new { success = false, message = "Hours must be between 1 and 180" });
                }

                // Calculate total amount
                decimal totalAmount = request.HoursWorked * user.HourlyRate;

                // Create new claim
                var claim = new Claim
                {
                    UserId = request.UserId,
                    ModuleId = request.ModuleId,
                    HoursWorked = request.HoursWorked,
                    HourlyRate = user.HourlyRate,
                    TotalAmount = totalAmount,
                    ClaimPeriod = request.ClaimPeriod,
                    AdditionalNotes = request.AdditionalNotes ?? string.Empty,
                    CurrentStatus = ClaimStatus.Submitted,
                    SubmissionDate = DateTime.Now
                };

                _context.Claims.Add(claim);
                await _context.SaveChangesAsync();

                // Add status history
                var statusHistory = new ClaimStatusHistory
                {
                    ClaimId = claim.ClaimId,
                    PreviousStatus = ClaimStatus.Draft,
                    NewStatus = ClaimStatus.Submitted,
                    ChangeDate = DateTime.Now,
                    ChangedBy = request.UserId,
                    Comments = "Claim submitted"
                };
                _context.ClaimStatusHistories.Add(statusHistory);

                // Handle file upload if present
                if (request.SupportingDocument != null && request.SupportingDocument.Length > 0)
                {
                    var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
                    Directory.CreateDirectory(uploadsPath);

                    var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(request.SupportingDocument.FileName)}";
                    var filePath = Path.Combine(uploadsPath, fileName);

                    // Encrypt and save using the service
                    var encryptedBytes = await _encryptionService.EncryptFileAsync(request.SupportingDocument);
                    await System.IO.File.WriteAllBytesAsync(filePath, encryptedBytes);

                    // Save document record
                    var document = new SupportingDocument
                    {
                        ClaimId = claim.ClaimId,
                        FileName = request.SupportingDocument.FileName,
                        FilePath = fileName,
                        FileSize = request.SupportingDocument.Length,
                        FileType = Path.GetExtension(request.SupportingDocument.FileName).TrimStart('.'),
                        Description = "Uploaded via API"
                    };
                    _context.SupportingDocuments.Add(document);
                }

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Claim submitted successfully",
                    claimId = claim.ClaimId,
                    totalAmount = totalAmount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting claim");
                return StatusCode(500, new { success = false, message = "An error occurred while submitting the claim" });
            }
        }

        // POST: api/v1/claims/calculate
        [HttpPost("calculate")]
        public async Task<IActionResult> CalculatePayment([FromBody] CalculateRequest request)
        {
            try
            {
                var user = await _context.Users.FindAsync(request.UserId);
                if (user == null)
                {
                    return BadRequest(new { success = false, message = "User not found" });
                }

                if (request.HoursWorked <= 0 || request.HoursWorked > 180)
                {
                    return BadRequest(new { success = false, message = "Hours must be between 1 and 180" });
                }

                decimal totalAmount = request.HoursWorked * user.HourlyRate;

                return Ok(new
                {
                    success = true,
                    hourlyRate = user.HourlyRate,
                    hoursWorked = request.HoursWorked,
                    totalAmount = totalAmount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating payment");
                return StatusCode(500, new { success = false, message = "An error occurred" });
            }
        }

        // GET: api/v1/claims/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetClaim(int id)
        {
            try
            {
                var claim = await _context.Claims
                    .Include(c => c.User)
                    .Include(c => c.Module)
                    .Include(c => c.SupportingDocuments)
                    .FirstOrDefaultAsync(c => c.ClaimId == id);

                if (claim == null)
                {
                    return NotFound(new { success = false, message = "Claim not found" });
                }

                return Ok(new
                {
                    success = true,
                    claim = new
                    {
                        claim.ClaimId,
                        claim.UserId,
                        LecturerName = $"{claim.User.FirstName} {claim.User.LastName}",
                        claim.ModuleId,
                        ModuleName = claim.Module?.ModuleName,
                        claim.HoursWorked,
                        claim.HourlyRate,
                        claim.TotalAmount,
                        claim.ClaimPeriod,
                        Status = claim.CurrentStatus.ToString(),
                        claim.AdditionalNotes,
                        claim.SubmissionDate,
                        DocumentCount = claim.SupportingDocuments?.Count ?? 0
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting claim {ClaimId}", id);
                return StatusCode(500, new { success = false, message = "An error occurred" });
            }
        }

        // GET: api/v1/claims/pending
        [HttpGet("pending")]
        public async Task<IActionResult> GetPendingClaims()
        {
            try
            {
                var claims = await _context.Claims
                    .Include(c => c.User)
                    .Include(c => c.Module)
                    .Where(c => c.CurrentStatus == ClaimStatus.Submitted)
                    .OrderBy(c => c.SubmissionDate)
                    .Select(c => new
                    {
                        c.ClaimId,
                        LecturerName = c.User.FirstName + " " + c.User.LastName,
                        ModuleName = c.Module != null ? c.Module.ModuleName : "N/A",
                        c.HoursWorked,
                        c.TotalAmount,
                        Status = c.CurrentStatus.ToString(),
                        c.SubmissionDate
                    })
                    .ToListAsync();

                return Ok(new { success = true, claims });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting pending claims");
                return StatusCode(500, new { success = false, message = "An error occurred" });
            }
        }
    }

    // Request models
    public class ClaimSubmitRequest
    {
        public int UserId { get; set; }
        public int ModuleId { get; set; }
        public decimal HoursWorked { get; set; }
        public string ClaimPeriod { get; set; } = string.Empty;
        public string? AdditionalNotes { get; set; }
        public IFormFile? SupportingDocument { get; set; }
    }

    public class CalculateRequest
    {
        public int UserId { get; set; }
        public decimal HoursWorked { get; set; }
    }
}
//--------------------------End Of File--------------------------//