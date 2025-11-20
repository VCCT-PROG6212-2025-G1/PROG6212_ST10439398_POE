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
        private readonly FileEncryptionService _encryptionService;
        private readonly ILogger<CoordinatorController> _logger;
        private readonly IHttpClientFactory _httpClientFactory;

        public CoordinatorController(
            CMCSContext context,
            FileEncryptionService encryptionService,
            ILogger<CoordinatorController> logger,
            IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _encryptionService = encryptionService;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }

        // GET: Coordinator/Dashboard
        public async Task<IActionResult> Dashboard()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var pendingClaims = await _context.Claims
                .Include(c => c.User)
                .Include(c => c.Module)
                .Include(c => c.SupportingDocuments)
                .Where(c => c.CurrentStatus == ClaimStatus.Submitted)
                .OrderBy(c => c.SubmissionDate)
                .ToListAsync();

            var viewModel = new CoordinatorDashboardViewModel
            {
                PendingClaims = pendingClaims,
                TotalPending = pendingClaims.Count,
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

        // POST: Coordinator/Verify/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Verify(int id, string? comments)
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
                        Comments = comments ?? "Verified by Coordinator"
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

        // POST: Coordinator/Reject/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id, string? comments)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            if (string.IsNullOrWhiteSpace(comments))
            {
                TempData["Error"] = "Please provide a reason for rejection.";
                return RedirectToAction(nameof(ViewClaim), new { id });
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
                        Comments = comments
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

        // GET: Coordinator/ClaimHistory
        public async Task<IActionResult> ClaimHistory()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var claims = await _context.Claims
                .Include(c => c.User)
                .Include(c => c.Module)
                .Where(c => c.CurrentStatus == ClaimStatus.UnderReview ||
                            c.CurrentStatus == ClaimStatus.Rejected ||
                            c.CurrentStatus == ClaimStatus.Approved)
                .OrderByDescending(c => c.SubmissionDate)
                .ToListAsync();

            return View(claims);
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
                var decryptedBytes = _encryptionService.Decrypt(encryptedBytes);

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
            return fileType.ToLower() switch
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