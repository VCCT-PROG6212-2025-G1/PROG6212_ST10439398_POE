//--------------------------Start Of File--------------------------//
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CMCS.Data;
using CMCS.Models;

namespace CMCS.Controllers.Api
{

    /// Micro API for Approvals automation
    /// Handles verify, approve, and reject operations

    [Route("api/v1/approvals")]
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

   
        /// Get all claims pending verification (for Coordinator)

        [HttpGet("pending-verification")]
        public async Task<ActionResult<IEnumerable<ApprovalClaimDto>>> GetPendingVerification()
        {
            try
            {
                var claims = await _context.Claims
                    .Include(c => c.User)
                    .Include(c => c.Module)
                    .Where(c => c.CurrentStatus == ClaimStatus.Submitted)
                    .OrderByDescending(c => c.SubmissionDate)
                    .Select(c => new ApprovalClaimDto
                    {
                        ClaimId = c.ClaimId,
                        LecturerName = c.User.FirstName + " " + c.User.LastName,
                        LecturerEmail = c.User.Email,
                        ModuleCode = c.Module.ModuleCode,
                        HoursWorked = c.HoursWorked,
                        TotalAmount = c.TotalAmount,
                        Status = c.CurrentStatus.ToString(),
                        SubmissionDate = c.SubmissionDate,
                        DaysPending = (int)(DateTime.Now - c.SubmissionDate).TotalDays
                    })
                    .ToListAsync();

                return Ok(claims);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving pending verification claims");
                return StatusCode(500, new { error = "Error retrieving claims" });
            }
        }


        /// Get all claims pending approval (for Manager)

        [HttpGet("pending-approval")]
        public async Task<ActionResult<IEnumerable<ApprovalClaimDto>>> GetPendingApproval()
        {
            try
            {
                var claims = await _context.Claims
                    .Include(c => c.User)
                    .Include(c => c.Module)
                    .Where(c => c.CurrentStatus == ClaimStatus.UnderReview)
                    .OrderByDescending(c => c.SubmissionDate)
                    .Select(c => new ApprovalClaimDto
                    {
                        ClaimId = c.ClaimId,
                        LecturerName = c.User.FirstName + " " + c.User.LastName,
                        LecturerEmail = c.User.Email,
                        ModuleCode = c.Module.ModuleCode,
                        HoursWorked = c.HoursWorked,
                        TotalAmount = c.TotalAmount,
                        Status = c.CurrentStatus.ToString(),
                        SubmissionDate = c.SubmissionDate,
                        DaysPending = (int)(DateTime.Now - c.SubmissionDate).TotalDays
                    })
                    .ToListAsync();

                return Ok(claims);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving pending approval claims");
                return StatusCode(500, new { error = "Error retrieving claims" });
            }
        }


        /// Verify a claim (Coordinator action)
        /// POST /api/v1/approvals/{id}/verify

        [HttpPost("{id}/verify")]
        public async Task<ActionResult<ApprovalResult>> VerifyClaim(int id, [FromBody] ApprovalRequest request)
        {
            try
            {
                var claim = await _context.Claims
                    .Include(c => c.User)
                    .FirstOrDefaultAsync(c => c.ClaimId == id);

                if (claim == null)
                {
                    return NotFound(new { error = $"Claim {id} not found" });
                }

                if (claim.CurrentStatus != ClaimStatus.Submitted)
                {
                    return BadRequest(new { error = "Only submitted claims can be verified" });
                }

                var previousStatus = claim.CurrentStatus;
                claim.CurrentStatus = ClaimStatus.UnderReview;
                claim.LastModified = DateTime.Now;

                _context.Entry(claim).State = EntityState.Modified;

                var statusHistory = new ClaimStatusHistory
                {
                    ClaimId = claim.ClaimId,
                    ChangedBy = request.UserId,
                    PreviousStatus = previousStatus,
                    NewStatus = ClaimStatus.UnderReview,
                    Comments = request.Comments ?? "Claim verified by Coordinator via API"
                };

                _context.ClaimStatusHistories.Add(statusHistory);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Claim {ClaimId} verified via API by user {UserId}", id, request.UserId);

                return Ok(new ApprovalResult
                {
                    ClaimId = claim.ClaimId,
                    NewStatus = "UnderReview",
                    Message = $"Claim CLC-{claim.ClaimId:D4} has been verified and sent to Manager for approval",
                    ProcessedAt = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying claim {ClaimId}", id);
                return StatusCode(500, new { error = "Error verifying claim" });
            }
        }


        /// Approve a claim (Manager action)
        /// POST /api/v1/approvals/{id}/approve
 
        [HttpPost("{id}/approve")]
        public async Task<ActionResult<ApprovalResult>> ApproveClaim(int id, [FromBody] ApprovalRequest request)
        {
            try
            {
                var claim = await _context.Claims
                    .Include(c => c.User)
                    .FirstOrDefaultAsync(c => c.ClaimId == id);

                if (claim == null)
                {
                    return NotFound(new { error = $"Claim {id} not found" });
                }

                if (claim.CurrentStatus != ClaimStatus.UnderReview)
                {
                    return BadRequest(new { error = "Only verified claims (Under Review) can be approved" });
                }

                var previousStatus = claim.CurrentStatus;
                claim.CurrentStatus = ClaimStatus.Approved;
                claim.LastModified = DateTime.Now;

                _context.Entry(claim).State = EntityState.Modified;

                var statusHistory = new ClaimStatusHistory
                {
                    ClaimId = claim.ClaimId,
                    ChangedBy = request.UserId,
                    PreviousStatus = previousStatus,
                    NewStatus = ClaimStatus.Approved,
                    Comments = request.Comments ?? "Claim approved by Manager via API"
                };

                _context.ClaimStatusHistories.Add(statusHistory);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Claim {ClaimId} approved via API by user {UserId}", id, request.UserId);

                return Ok(new ApprovalResult
                {
                    ClaimId = claim.ClaimId,
                    NewStatus = "Approved",
                    Message = $"Claim CLC-{claim.ClaimId:D4} has been approved. Amount: R{claim.TotalAmount:N2}",
                    ProcessedAt = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving claim {ClaimId}", id);
                return StatusCode(500, new { error = "Error approving claim" });
            }
        }


        /// Reject a claim (Coordinator or Manager action)
        /// POST /api/v1/approvals/{id}/reject

        [HttpPost("{id}/reject")]
        public async Task<ActionResult<ApprovalResult>> RejectClaim(int id, [FromBody] RejectRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Reason))
                {
                    return BadRequest(new { error = "Rejection reason is required" });
                }

                var claim = await _context.Claims
                    .Include(c => c.User)
                    .FirstOrDefaultAsync(c => c.ClaimId == id);

                if (claim == null)
                {
                    return NotFound(new { error = $"Claim {id} not found" });
                }

                if (claim.CurrentStatus != ClaimStatus.Submitted && claim.CurrentStatus != ClaimStatus.UnderReview)
                {
                    return BadRequest(new { error = "Only pending claims can be rejected" });
                }

                var previousStatus = claim.CurrentStatus;
                claim.CurrentStatus = ClaimStatus.Rejected;
                claim.LastModified = DateTime.Now;

                _context.Entry(claim).State = EntityState.Modified;

                var statusHistory = new ClaimStatusHistory
                {
                    ClaimId = claim.ClaimId,
                    ChangedBy = request.UserId,
                    PreviousStatus = previousStatus,
                    NewStatus = ClaimStatus.Rejected,
                    Comments = $"Rejected via API: {request.Reason}"
                };

                _context.ClaimStatusHistories.Add(statusHistory);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Claim {ClaimId} rejected via API by user {UserId}. Reason: {Reason}",
                    id, request.UserId, request.Reason);

                return Ok(new ApprovalResult
                {
                    ClaimId = claim.ClaimId,
                    NewStatus = "Rejected",
                    Message = $"Claim CLC-{claim.ClaimId:D4} has been rejected",
                    ProcessedAt = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting claim {ClaimId}", id);
                return StatusCode(500, new { error = "Error rejecting claim" });
            }
        }

 
        /// Finalise approved claims for HR processing
        /// POST /api/v1/approvals/{id}/finalise

        [HttpPost("{id}/finalise")]
        public async Task<ActionResult<ApprovalResult>> FinaliseClaim(int id, [FromBody] ApprovalRequest request)
        {
            try
            {
                var claim = await _context.Claims
                    .Include(c => c.User)
                    .FirstOrDefaultAsync(c => c.ClaimId == id);

                if (claim == null)
                {
                    return NotFound(new { error = $"Claim {id} not found" });
                }

                if (claim.CurrentStatus != ClaimStatus.Approved)
                {
                    return BadRequest(new { error = "Only approved claims can be finalised" });
                }

                var previousStatus = claim.CurrentStatus;
                claim.CurrentStatus = ClaimStatus.PaymentProcessing;
                claim.LastModified = DateTime.Now;

                _context.Entry(claim).State = EntityState.Modified;

                var statusHistory = new ClaimStatusHistory
                {
                    ClaimId = claim.ClaimId,
                    ChangedBy = request.UserId,
                    PreviousStatus = previousStatus,
                    NewStatus = ClaimStatus.PaymentProcessing,
                    Comments = request.Comments ?? "Claim finalised for payment processing by HR"
                };

                _context.ClaimStatusHistories.Add(statusHistory);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Claim {ClaimId} finalised via API by HR user {UserId}", id, request.UserId);

                return Ok(new ApprovalResult
                {
                    ClaimId = claim.ClaimId,
                    NewStatus = "PaymentProcessing",
                    Message = $"Claim CLC-{claim.ClaimId:D4} has been finalised for payment processing",
                    ProcessedAt = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finalising claim {ClaimId}", id);
                return StatusCode(500, new { error = "Error finalising claim" });
            }
        }
    }

    #region DTOs

    public class ApprovalClaimDto
    {
        public int ClaimId { get; set; }
        public string LecturerName { get; set; }
        public string LecturerEmail { get; set; }
        public string ModuleCode { get; set; }
        public decimal HoursWorked { get; set; }
        public decimal TotalAmount { get; set; }
        public string Status { get; set; }
        public DateTime SubmissionDate { get; set; }
        public int DaysPending { get; set; }
    }

    public class ApprovalRequest
    {
        public int UserId { get; set; }
        public string? Comments { get; set; }
    }

    public class RejectRequest
    {
        public int UserId { get; set; }
        public string Reason { get; set; }
    }

    public class ApprovalResult
    {
        public int ClaimId { get; set; }
        public string NewStatus { get; set; }
        public string Message { get; set; }
        public DateTime ProcessedAt { get; set; }
    }

    #endregion
}
//--------------------------End Of File--------------------------//