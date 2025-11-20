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
        private readonly FileEncryptionService _encryptionService;
        private readonly ILogger<ManagerController> _logger;
        private readonly IHttpClientFactory _httpClientFactory;

        public ManagerController(
            CMCSContext context,
            FileEncryptionService encryptionService,
            ILogger<ManagerController> logger,
            IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _encryptionService = encryptionService;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }

        // GET: Manager/Dashboard
        public async Task<IActionResult> Dashboard()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // Manager only sees VERIFIED claims (not pending)
            var verifiedClaims = await _context.Claims
                .Include(c => c.User)
                .Include(c => c.Module)
                .Include(c => c.SupportingDocuments)
                .Where(c => c.Status == "Verified")
                .OrderBy(c => c.SubmissionDate)
                .ToListAsync();

            var viewModel = new ManagerDashboardViewModel
            {
                VerifiedClaims = verifiedClaims,
                TotalVerified = verifiedClaims.Count,
                TotalApproved = await _context.Claims.CountAsync(c => c.Status == "Approved"),
                TotalRejected = await _context.Claims.CountAsync(c => c.Status == "Rejected" && c.RejectedByUserId != null)
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

        // POST: Manager/Approve/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id, string? comments)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                // Call API to approve claim
                var client = _httpClientFactory.CreateClient();
                var baseUrl = $"{Request.Scheme}://{Request.Host}";

                var response = await client.PostAsJsonAsync($"{baseUrl}/api/v1/approvals/{id}/approve", new
                {
                    userId = userId.Value,
                    comments = comments ?? "Approved by Manager"
                });

                if (response.IsSuccessStatusCode)
                {
                    TempData["Success"] = "Claim has been approved for payment.";
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    TempData["Error"] = $"Failed to approve claim: {error}";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving claim {ClaimId}", id);

                // Fallback to direct database update if API fails
                var claim = await _context.Claims.FindAsync(id);
                if (claim != null && claim.Status == "Verified")
                {
                    claim.Status = "Approved";
                    claim.ApprovedByUserId = userId;
                    claim.ApprovedAt = DateTime.Now;

                    var statusHistory = new ClaimStatusHistory
                    {
                        ClaimId = id,
                        Status = "Approved",
                        ChangedAt = DateTime.Now,
                        ChangedByUserId = userId.Value,
                        Comments = comments ?? "Approved by Manager"
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

            return RedirectToAction(nameof(Dashboard));
        }

        // POST: Manager/Reject/5
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
                // Call API to reject claim
                var client = _httpClientFactory.CreateClient();
                var baseUrl = $"{Request.Scheme}://{Request.Host}";

                var response = await client.PostAsJsonAsync($"{baseUrl}/api/v1/approvals/{id}/reject", new
                {
                    userId = userId.Value,
                    comments = comments
                });

                if (response.IsSuccessStatusCode)
                {
                    TempData["Success"] = "Claim has been rejected.";
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    TempData["Error"] = $"Failed to reject claim: {error}";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting claim {ClaimId}", id);

                // Fallback to direct database update if API fails
                var claim = await _context.Claims.FindAsync(id);
                if (claim != null && claim.Status == "Verified")
                {
                    claim.Status = "Rejected";
                    claim.RejectedByUserId = userId;
                    claim.RejectedAt = DateTime.Now;
                    claim.RejectionReason = comments;

                    var statusHistory = new ClaimStatusHistory
                    {
                        ClaimId = id,
                        Status = "Rejected",
                        ChangedAt = DateTime.Now,
                        ChangedByUserId = userId.Value,
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

            return RedirectToAction(nameof(Dashboard));
        }

        // GET: Manager/ClaimHistory
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
                .Where(c => c.Status == "Approved" || (c.Status == "Rejected" && c.RejectedByUserId != null))
                .OrderByDescending(c => c.SubmissionDate)
                .ToListAsync();

            return View(claims);
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
                var decryptedBytes = _encryptionService.Decrypt(encryptedBytes);

                if (decryptedBytes == null)
                {
                    TempData["Error"] = "Unable to decrypt file. File may be corrupted.";
                    return RedirectToAction(nameof(ViewClaim), new { id = document.ClaimId });
                }

                return File(decryptedBytes, document.ContentType, document.FileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading document {DocumentId}", documentId);
                TempData["Error"] = "An error occurred downloading the file.";
                return RedirectToAction(nameof(Dashboard));
            }
        }

        // GET: Manager/Reports
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
                .Where(c => c.Status == "Approved")
                .OrderByDescending(c => c.ApprovedAt)
                .ToListAsync();

            return View(approvedClaims);
        }
    }
}
//--------------------------End Of File--------------------------/