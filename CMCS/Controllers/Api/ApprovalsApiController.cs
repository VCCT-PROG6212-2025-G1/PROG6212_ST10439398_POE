//--------------------------Start Of File--------------------------//
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CMCS.Data;
using CMCS.Models;

namespace CMCS.Controllers.Api
{
    [Route("api/v1/[controller]")]
    [ApiController]
    public class ApprovalsApiController : ControllerBase
    {
        private readonly CMCSContext _context;
        private readonly ILogger<ApprovalsApiController> _logger;

        public ApprovalsApiController(CMCSContext context, ILogger<ApprovalsApiController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // POST: api/v1/approvals/{id}/verify
        [HttpPost("{id}/verify")]
        public async Task<IActionResult> VerifyClaim(int id, [FromBody] ApprovalRequest request)
        {
            try
            {
                var claim = await _context.Claims.FindAsync(id);
                if (claim == null)
                {
                    return NotFound(new { success = false, message = "Claim not found" });
                }

                if (claim.CurrentStatus != ClaimStatus.Submitted)
                {
                    return BadRequest(new { success = false, message = "Claim is not in Submitted status" });
                }

                var previousStatus = claim.CurrentStatus;
                claim.CurrentStatus = ClaimStatus.UnderReview;
                claim.LastModified = DateTime.Now;

                // Add status history
                var statusHistory = new ClaimStatusHistory
                {
                    ClaimId = id,
                    PreviousStatus = previousStatus,
                    NewStatus = ClaimStatus.UnderReview,
                    ChangeDate = DateTime.Now,
                    ChangedBy = request.UserId,
                    Comments = request.Comments ?? "Verified by Coordinator"
                };
                _context.ClaimStatusHistories.Add(statusHistory);

                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Claim verified successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying claim {ClaimId}", id);
                return StatusCode(500, new { success = false, message = "An error occurred" });
            }
        }

        // POST: api/v1/approvals/{id}/approve
        [HttpPost("{id}/approve")]
        public async Task<IActionResult> ApproveClaim(int id, [FromBody] ApprovalRequest request)
        {
            try
            {
                var claim = await _context.Claims.FindAsync(id);
                if (claim == null)
                {
                    return NotFound(new { success = false, message = "Claim not found" });
                }

                if (claim.CurrentStatus != ClaimStatus.UnderReview)
                {
                    return BadRequest(new { success = false, message = "Claim must be verified before approval" });
                }

                var previousStatus = claim.CurrentStatus;
                claim.CurrentStatus = ClaimStatus.Approved;
                claim.LastModified = DateTime.Now;

                // Add status history
                var statusHistory = new ClaimStatusHistory
                {
                    ClaimId = id,
                    PreviousStatus = previousStatus,
                    NewStatus = ClaimStatus.Approved,
                    ChangeDate = DateTime.Now,
                    ChangedBy = request.UserId,
                    Comments = request.Comments ?? "Approved by Manager"
                };
                _context.ClaimStatusHistories.Add(statusHistory);

                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Claim approved successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving claim {ClaimId}", id);
                return StatusCode(500, new { success = false, message = "An error occurred" });
            }
        }

        // POST: api/v1/approvals/{id}/reject
        [HttpPost("{id}/reject")]
        public async Task<IActionResult> RejectClaim(int id, [FromBody] ApprovalRequest request)
        {
            try
            {
                var claim = await _context.Claims.FindAsync(id);
                if (claim == null)
                {
                    return NotFound(new { success = false, message = "Claim not found" });
                }

                if (claim.CurrentStatus != ClaimStatus.Submitted && claim.CurrentStatus != ClaimStatus.UnderReview)
                {
                    return BadRequest(new { success = false, message = "Claim cannot be rejected in current status" });
                }

                var previousStatus = claim.CurrentStatus;
                claim.CurrentStatus = ClaimStatus.Rejected;
                claim.LastModified = DateTime.Now;

                // Add status history
                var statusHistory = new ClaimStatusHistory
                {
                    ClaimId = id,
                    PreviousStatus = previousStatus,
                    NewStatus = ClaimStatus.Rejected,
                    ChangeDate = DateTime.Now,
                    ChangedBy = request.UserId,
                    Comments = request.Comments ?? "Rejected"
                };
                _context.ClaimStatusHistories.Add(statusHistory);

                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Claim rejected successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting claim {ClaimId}", id);
                return StatusCode(500, new { success = false, message = "An error occurred" });
            }
        }

        // GET: api/v1/approvals/pending
        [HttpGet("pending")]
        public async Task<IActionResult> GetPendingApprovals()
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
                _logger.LogError(ex, "Error getting pending approvals");
                return StatusCode(500, new { success = false, message = "An error occurred" });
            }
        }

        // GET: api/v1/approvals/verified
        [HttpGet("verified")]
        public async Task<IActionResult> GetVerifiedClaims()
        {
            try
            {
                var claims = await _context.Claims
                    .Include(c => c.User)
                    .Include(c => c.Module)
                    .Where(c => c.CurrentStatus == ClaimStatus.UnderReview)
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
                _logger.LogError(ex, "Error getting verified claims");
                return StatusCode(500, new { success = false, message = "An error occurred" });
            }
        }
    }

    public class ApprovalRequest
    {
        public int UserId { get; set; }
        public string? Comments { get; set; }
    }
}
//--------------------------End Of File--------------------------//