//--------------------------Start Of File--------------------------//
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CMCS.Data;
using CMCS.Models;
using CMCS.ViewModels;
using CMCS.Attributes;
using CMCS.Services;

namespace CMCS.Controllers
{
    [SessionAuthorize("Manager")]
    public class ManagerController : Controller
    {
        private readonly CMCSContext _context;
        private readonly IFileEncryptionService _encryptionService;
        private readonly ILogger<ManagerController> _logger;

        public ManagerController(
            CMCSContext context,
            IFileEncryptionService encryptionService,
            ILogger<ManagerController> logger)
        {
            _context = context;
            _encryptionService = encryptionService;
            _logger = logger;
        }

        // GET: Manager/Dashboard
        public async Task<IActionResult> Dashboard()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // ✅ FIXED: Manager only sees VERIFIED claims (UnderReview status)
            var claimsForApproval = await _context.Claims
                .Include(c => c.User)
                .Include(c => c.Module)
                .Include(c => c.SupportingDocuments)
                .Where(c => c.CurrentStatus == ClaimStatus.UnderReview)
                .OrderBy(c => c.SubmissionDate)
                .ToListAsync();

            var startOfWeek = DateTime.Now.Date.AddDays(-(int)DateTime.Now.DayOfWeek);
            var today = DateTime.Now.Date;

            // ✅ FIXED: Calculate correct statistics
            var viewModel = new ManagerDashboardViewModel
            {
                ClaimsForApproval = claimsForApproval,
                VerifiedClaims = claimsForApproval,
                
                // ✅ FIXED: Pending Approval = claims with UnderReview status
                PendingApproval = claimsForApproval.Count,
                
                // ✅ FIXED: Approved Today = claims approved today (changed to Approved status today)
                ApprovedToday = await _context.ClaimStatusHistories
                    .Where(h => h.NewStatus == ClaimStatus.Approved &&
                                h.ChangeDate.Date == today)
                    .Select(h => h.ClaimId)
                    .Distinct()
                    .CountAsync(),
                
                // ✅ FIXED: Urgent Claims = verified claims over 5 days old
                UrgentClaims = claimsForApproval.Count(c => (DateTime.Now - c.SubmissionDate).Days > 5),
                
                // ✅ FIXED: Total This Week = APPROVED amount this week (in Rand)
                TotalThisWeek = await _context.Claims
                    .Where(c => c.CurrentStatus == ClaimStatus.Approved &&
                                c.LastModified.HasValue &&
                                c.LastModified.Value >= startOfWeek)
                    .SumAsync(c => (decimal?)c.TotalAmount) ?? 0,
                
                TotalVerified = claimsForApproval.Count,
                TotalApproved = await _context.Claims.CountAsync(c => c.CurrentStatus == ClaimStatus.Approved),
                TotalRejected = await _context.Claims.CountAsync(c => c.CurrentStatus == ClaimStatus.Rejected)
            };

            return View(viewModel);
        }

        // GET: Manager/ViewClaim/5
        public async Task<IActionResult> ViewClaim(int id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var claim = await _context.Claims
                .Include(c => c.User)
                .Include(c => c.Module)
                .Include(c => c.SupportingDocuments)
                .Include(c => c.StatusHistory)
                .FirstOrDefaultAsync(c => c.ClaimId == id);

            if (claim == null)
            {
                return NotFound();
            }

            return View(claim);
        }

        // POST: Manager/ApproveClaim
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveClaim(int id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var claim = await _context.Claims.FindAsync(id);
                if (claim != null && claim.CurrentStatus == ClaimStatus.UnderReview)
                {
                    var previousStatus = claim.CurrentStatus;
                    claim.CurrentStatus = ClaimStatus.Approved;
                    claim.LastModified = DateTime.Now;

                    var statusHistory = new ClaimStatusHistory
                    {
                        ClaimId = id,
                        PreviousStatus = previousStatus,
                        NewStatus = ClaimStatus.Approved,
                        ChangeDate = DateTime.Now,
                        ChangedBy = userId.Value,
                        Comments = "Approved by Manager"
                    };
                    _context.ClaimStatusHistories.Add(statusHistory);
                    await _context.SaveChangesAsync();

                    TempData["Success"] = "Claim has been approved for payment.";
                }
                else
                {
                    TempData["Error"] = "Failed to approve claim. Claim must be verified first.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving claim {ClaimId}", id);
                TempData["Error"] = "An error occurred while approving the claim.";
            }

            return RedirectToAction(nameof(Dashboard));
        }

        // POST: Manager/RejectClaim
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectClaim(int id, string reason)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                TempData["Error"] = "Please provide a reason for rejection.";
                return RedirectToAction(nameof(Dashboard));
            }

            try
            {
                var claim = await _context.Claims.FindAsync(id);
                if (claim != null && claim.CurrentStatus == ClaimStatus.UnderReview)
                {
                    var previousStatus = claim.CurrentStatus;
                    claim.CurrentStatus = ClaimStatus.Rejected;
                    claim.LastModified = DateTime.Now;

                    var statusHistory = new ClaimStatusHistory
                    {
                        ClaimId = id,
                        PreviousStatus = previousStatus,
                        NewStatus = ClaimStatus.Rejected,
                        ChangeDate = DateTime.Now,
                        ChangedBy = userId.Value,
                        Comments = reason
                    };
                    _context.ClaimStatusHistories.Add(statusHistory);
                    await _context.SaveChangesAsync();

                    TempData["Success"] = "Claim has been rejected.";
                }
                else
                {
                    TempData["Error"] = "Failed to reject claim.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting claim {ClaimId}", id);
                TempData["Error"] = "An error occurred while rejecting the claim.";
            }

            return RedirectToAction(nameof(Dashboard));
        }

        // POST: Manager/BulkApprove
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkApprove(int[] claimIds)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                int approvedCount = 0;
                foreach (var claimId in claimIds)
                {
                    var claim = await _context.Claims.FindAsync(claimId);
                    if (claim != null && claim.CurrentStatus == ClaimStatus.UnderReview)
                    {
                        var previousStatus = claim.CurrentStatus;
                        claim.CurrentStatus = ClaimStatus.Approved;
                        claim.LastModified = DateTime.Now;

                        var statusHistory = new ClaimStatusHistory
                        {
                            ClaimId = claimId,
                            PreviousStatus = previousStatus,
                            NewStatus = ClaimStatus.Approved,
                            ChangeDate = DateTime.Now,
                            ChangedBy = userId.Value,
                            Comments = "Bulk approved by Manager"
                        };
                        _context.ClaimStatusHistories.Add(statusHistory);
                        approvedCount++;
                    }
                }
                await _context.SaveChangesAsync();

                TempData["Success"] = $"{approvedCount} claim(s) have been approved for payment.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error bulk approving claims");
                TempData["Error"] = "An error occurred while approving claims.";
            }

            return RedirectToAction(nameof(Dashboard));
        }

        // POST: Manager/BulkReject
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkReject(int[] ClaimIds, string Reason)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            if (string.IsNullOrWhiteSpace(Reason))
            {
                TempData["Error"] = "Please provide a reason for rejection.";
                return RedirectToAction(nameof(Dashboard));
            }

            try
            {
                int rejectedCount = 0;
                foreach (var claimId in ClaimIds)
                {
                    var claim = await _context.Claims.FindAsync(claimId);
                    if (claim != null && claim.CurrentStatus == ClaimStatus.UnderReview)
                    {
                        var previousStatus = claim.CurrentStatus;
                        claim.CurrentStatus = ClaimStatus.Rejected;
                        claim.LastModified = DateTime.Now;

                        var statusHistory = new ClaimStatusHistory
                        {
                            ClaimId = claimId,
                            PreviousStatus = previousStatus,
                            NewStatus = ClaimStatus.Rejected,
                            ChangeDate = DateTime.Now,
                            ChangedBy = userId.Value,
                            Comments = Reason
                        };
                        _context.ClaimStatusHistories.Add(statusHistory);
                        rejectedCount++;
                    }
                }
                await _context.SaveChangesAsync();

                TempData["Success"] = $"{rejectedCount} claim(s) have been rejected.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error bulk rejecting claims");
                TempData["Error"] = "An error occurred while rejecting claims.";
            }

            return RedirectToAction(nameof(Dashboard));
        }

        // GET: Manager/DownloadDocument/5
        public async Task<IActionResult> DownloadDocument(int documentId)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var document = await _context.SupportingDocuments
                    .Include(d => d.Claim)
                    .FirstOrDefaultAsync(d => d.DocumentId == documentId);

                if (document == null)
                {
                    TempData["Error"] = "Document not found.";
                    return RedirectToAction(nameof(Dashboard));
                }

                var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", document.FilePath);

                if (!System.IO.File.Exists(filePath))
                {
                    TempData["Error"] = "File not found on server.";
                    return RedirectToAction(nameof(ViewClaim), new { id = document.ClaimId });
                }

                // Read and decrypt file
                var encryptedBytes = await System.IO.File.ReadAllBytesAsync(filePath);
                var decryptedBytes = _encryptionService.DecryptFile(encryptedBytes);

                if (decryptedBytes == null)
                {
                    TempData["Error"] = "Unable to decrypt file. File may be corrupted.";
                    return RedirectToAction(nameof(ViewClaim), new { id = document.ClaimId });
                }

                // Determine content type
                var contentType = GetContentType(document.FileType);
                return File(decryptedBytes, contentType, document.FileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading document {DocumentId}", documentId);
                TempData["Error"] = "An error occurred downloading the file.";
                return RedirectToAction(nameof(Dashboard));
            }
        }

        private string GetContentType(string fileType)
        {
            return fileType?.ToLower() switch
            {
                "pdf" => "application/pdf",
                "doc" => "application/msword",
                "docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                "xls" => "application/vnd.ms-excel",
                "xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "png" => "image/png",
                "jpg" or "jpeg" => "image/jpeg",
                "gif" => "image/gif",
                _ => "application/octet-stream"
            };
        }
    }
}
//--------------------------End Of File--------------------------//