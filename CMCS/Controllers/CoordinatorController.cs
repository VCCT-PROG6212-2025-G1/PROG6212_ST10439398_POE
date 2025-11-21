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
    [SessionAuthorize("Coordinator")]
    public class CoordinatorController : Controller
    {
        private readonly CMCSContext _context;
        private readonly IFileEncryptionService _encryptionService;
        private readonly ILogger<CoordinatorController> _logger;

        public CoordinatorController(
            CMCSContext context,
            IFileEncryptionService encryptionService,
            ILogger<CoordinatorController> logger)
        {
            _context = context;
            _encryptionService = encryptionService;
            _logger = logger;
        }

        // GET: Coordinator/Dashboard
        public async Task<IActionResult> Dashboard()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // ? FIXED: Get claims awaiting coordinator verification
            var claimsForReview = await _context.Claims
                .Include(c => c.User)
                .Include(c => c.Module)
                .Include(c => c.SupportingDocuments)
                .Where(c => c.CurrentStatus == ClaimStatus.Submitted)
                .OrderBy(c => c.SubmissionDate)
                .ToListAsync();

            var startOfWeek = DateTime.Now.Date.AddDays(-(int)DateTime.Now.DayOfWeek);
            var today = DateTime.Now.Date;

            // ? FIXED: Calculate correct statistics
            var viewModel = new CoordinatorDashboardViewModel
            {
                ClaimsForReview = claimsForReview,
                PendingClaims = claimsForReview,
                
                // ? FIXED: Pending Review = claims with Submitted status
                PendingReview = claimsForReview.Count,
                
                // ? FIXED: Approved Today = claims verified today (changed to UnderReview status today)
                ApprovedToday = await _context.ClaimStatusHistories
                    .Where(h => h.NewStatus == ClaimStatus.UnderReview &&
                                h.ChangeDate.Date == today)
                    .Select(h => h.ClaimId)
                    .Distinct()
                    .CountAsync(),
                
                // ? FIXED: Urgent Claims = submitted claims over 5 days old
                UrgentClaims = claimsForReview.Count(c => (DateTime.Now - c.SubmissionDate).Days > 5),
                
                // ? FIXED: Total This Week = APPROVED amount this week (verified by you, approved by manager)
                TotalThisWeek = await _context.Claims
                    .Where(c => c.CurrentStatus == ClaimStatus.Approved &&
                                c.LastModified.HasValue &&
                                c.LastModified.Value >= startOfWeek)
                    .SumAsync(c => (decimal?)c.TotalAmount) ?? 0,
                
                TotalPending = claimsForReview.Count,
                TotalVerified = await _context.Claims.CountAsync(c => c.CurrentStatus == ClaimStatus.UnderReview),
                TotalRejected = await _context.Claims.CountAsync(c => c.CurrentStatus == ClaimStatus.Rejected)
            };

            return View(viewModel);
        }

        // GET: Coordinator/ViewClaim/5
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

        // POST: Coordinator/VerifyClaim
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyClaim(int id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var claim = await _context.Claims.FindAsync(id);
                if (claim != null && claim.CurrentStatus == ClaimStatus.Submitted)
                {
                    var previousStatus = claim.CurrentStatus;
                    claim.CurrentStatus = ClaimStatus.UnderReview;
                    claim.LastModified = DateTime.Now;

                    var statusHistory = new ClaimStatusHistory
                    {
                        ClaimId = id,
                        PreviousStatus = previousStatus,
                        NewStatus = ClaimStatus.UnderReview,
                        ChangeDate = DateTime.Now,
                        ChangedBy = userId.Value,
                        Comments = "Verified by Coordinator"
                    };
                    _context.ClaimStatusHistories.Add(statusHistory);
                    await _context.SaveChangesAsync();

                    TempData["Success"] = "Claim has been verified and forwarded to Manager for approval.";
                }
                else
                {
                    TempData["Error"] = "Failed to verify claim.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying claim {ClaimId}", id);
                TempData["Error"] = "An error occurred while verifying the claim.";
            }

            return RedirectToAction(nameof(Dashboard));
        }

        // POST: Coordinator/RejectClaim
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
                if (claim != null && claim.CurrentStatus == ClaimStatus.Submitted)
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

        // POST: Coordinator/BulkVerify
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkVerify(int[] claimIds)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                int verifiedCount = 0;
                foreach (var claimId in claimIds)
                {
                    var claim = await _context.Claims.FindAsync(claimId);
                    if (claim != null && claim.CurrentStatus == ClaimStatus.Submitted)
                    {
                        var previousStatus = claim.CurrentStatus;
                        claim.CurrentStatus = ClaimStatus.UnderReview;
                        claim.LastModified = DateTime.Now;

                        var statusHistory = new ClaimStatusHistory
                        {
                            ClaimId = claimId,
                            PreviousStatus = previousStatus,
                            NewStatus = ClaimStatus.UnderReview,
                            ChangeDate = DateTime.Now,
                            ChangedBy = userId.Value,
                            Comments = "Bulk verified by Coordinator"
                        };
                        _context.ClaimStatusHistories.Add(statusHistory);
                        verifiedCount++;
                    }
                }
                await _context.SaveChangesAsync();

                TempData["Success"] = $"{verifiedCount} claim(s) have been verified and forwarded to Manager.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error bulk verifying claims");
                TempData["Error"] = "An error occurred while verifying claims.";
            }

            return RedirectToAction(nameof(Dashboard));
        }

        // POST: Coordinator/BulkReject
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
                    if (claim != null && claim.CurrentStatus == ClaimStatus.Submitted)
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

        // GET: Coordinator/DownloadDocument/5
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