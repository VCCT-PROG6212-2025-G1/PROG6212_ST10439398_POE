//--------------------------Start Of File--------------------------//
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using CMCS.Models;
using CMCS.Data;
using CMCS.ViewModels;
using CMCS.Services;

namespace CMCS.Controllers
{
    [Authorize(Roles = "Manager")]
    public class ManagerController : Controller
    {
        private readonly CMCSContext _context;
        private readonly ILogger<ManagerController> _logger;
        private readonly IFileEncryptionService _encryptionService;
        private readonly IWebHostEnvironment _environment;

        public ManagerController(CMCSContext context, ILogger<ManagerController> logger, IFileEncryptionService encryptionService, IWebHostEnvironment environment)
        {
            _context = context;
            _logger = logger;
            _encryptionService = encryptionService;
            _environment = environment;
        }

        public async Task<IActionResult> Dashboard(string filter = "all")
        {
            try
            {
                var today = DateTime.Today;
                var startOfWeek = today.AddDays(-(int)today.DayOfWeek);

                var viewModel = new ManagerDashboardViewModel
                {
                    PendingApproval = 0,
                    ApprovedToday = 0,
                    UrgentClaims = 0,
                    TotalThisWeek = 0,
                    ClaimsForApproval = new List<Claim>()
                };

                try
                {
                    // Manager sees claims that have been verified (UnderReview status)
                    viewModel.PendingApproval = await _context.Claims
                        .Where(c => c.CurrentStatus == ClaimStatus.UnderReview)
                        .CountAsync();

                    viewModel.ApprovedToday = await _context.Claims
                        .Include(c => c.StatusHistory)
                        .Where(c => c.CurrentStatus == ClaimStatus.Approved &&
                                   c.StatusHistory.Any(sh => sh.NewStatus == ClaimStatus.Approved &&
                                                            sh.ChangeDate.Date == today))
                        .CountAsync();

                    var allPendingClaims = await _context.Claims
                        .Where(c => c.CurrentStatus == ClaimStatus.UnderReview)
                        .ToListAsync();
                    viewModel.UrgentClaims = allPendingClaims.Count(c => (DateTime.Now - c.SubmissionDate).Days > 5);

                    viewModel.TotalThisWeek = await _context.Claims
                        .Include(c => c.StatusHistory)
                        .Where(c => c.CurrentStatus == ClaimStatus.Approved &&
                                   c.StatusHistory.Any(sh => sh.NewStatus == ClaimStatus.Approved &&
                                                            sh.ChangeDate >= startOfWeek &&
                                                            sh.ChangeDate <= DateTime.Now))
                        .SumAsync(c => (decimal?)c.TotalAmount) ?? 0;

                    // Manager sees only UnderReview claims (verified by Coordinator)
                    var query = _context.Claims
                        .Include(c => c.User)
                        .Include(c => c.Module)
                        .Include(c => c.StatusHistory)
                        .Where(c => c.CurrentStatus == ClaimStatus.UnderReview);

                    switch (filter.ToLower())
                    {
                        case "urgent":
                            var allClaims = await query.ToListAsync();
                            viewModel.ClaimsForApproval = allClaims
                                .Where(c => (DateTime.Now - c.SubmissionDate).Days > 5)
                                .OrderByDescending(c => c.SubmissionDate)
                                .ToList();
                            break;

                        case "thisweek":
                            viewModel.ClaimsForApproval = await query
                                .Where(c => c.SubmissionDate >= startOfWeek)
                                .OrderByDescending(c => c.SubmissionDate)
                                .ToListAsync();
                            break;

                        case "bymodule":
                            viewModel.ClaimsForApproval = await query
                                .OrderBy(c => c.Module.ModuleCode)
                                .ThenByDescending(c => c.SubmissionDate)
                                .ToListAsync();
                            break;

                        default:
                            viewModel.ClaimsForApproval = await query
                                .OrderByDescending(c => c.SubmissionDate)
                                .ToListAsync();
                            break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error fetching manager dashboard data");
                    TempData["Warning"] = "Some dashboard data could not be loaded.";
                }

                ViewBag.CurrentFilter = filter;
                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical error loading manager dashboard");
                TempData["Error"] = "An error occurred loading the dashboard. Please try again.";
                return View(new ManagerDashboardViewModel
                {
                    ClaimsForApproval = new List<Claim>()
                });
            }
        }

        // Manager can APPROVE a claim
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveClaim(int id)
        {
            try
            {
                var claim = await _context.Claims
                    .Include(c => c.User)
                    .Include(c => c.Module)
                    .FirstOrDefaultAsync(c => c.ClaimId == id);

                if (claim == null)
                {
                    TempData["Error"] = "Claim not found.";
                    return RedirectToAction(nameof(Dashboard));
                }

                if (claim.CurrentStatus != ClaimStatus.UnderReview)
                {
                    TempData["Error"] = "Only verified claims (Under Review) can be approved.";
                    return RedirectToAction(nameof(Dashboard));
                }

                var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier).Value);

                var previousStatus = claim.CurrentStatus;
                claim.CurrentStatus = ClaimStatus.Approved;
                claim.LastModified = DateTime.Now;

                _context.Entry(claim).State = EntityState.Modified;

                var statusHistory = new ClaimStatusHistory
                {
                    ClaimId = claim.ClaimId,
                    ChangedBy = userId,
                    PreviousStatus = previousStatus,
                    NewStatus = ClaimStatus.Approved,
                    Comments = "Claim approved by Academic Manager"
                };

                _context.ClaimStatusHistories.Add(statusHistory);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Claim #CLC-{claim.ClaimId.ToString("D4")} for {claim.User.FirstName} {claim.User.LastName} has been approved! Amount: R{claim.TotalAmount:N2}";
                _logger.LogInformation("Claim {ClaimId} approved by manager {UserId}", claim.ClaimId, userId);

                return RedirectToAction(nameof(Dashboard));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving claim {ClaimId}", id);
                TempData["Error"] = "An error occurred while approving the claim. Please try again.";
                return RedirectToAction(nameof(Dashboard));
            }
        }

        // Manager can REJECT a claim
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectClaim(int id, string reason)
        {
            try
            {
                var claim = await _context.Claims
                    .Include(c => c.User)
                    .Include(c => c.Module)
                    .FirstOrDefaultAsync(c => c.ClaimId == id);

                if (claim == null)
                {
                    TempData["Error"] = "Claim not found.";
                    return RedirectToAction(nameof(Dashboard));
                }

                if (claim.CurrentStatus != ClaimStatus.UnderReview)
                {
                    TempData["Error"] = "Only verified claims (Under Review) can be rejected by Manager.";
                    return RedirectToAction(nameof(Dashboard));
                }

                if (string.IsNullOrWhiteSpace(reason))
                {
                    TempData["Error"] = "Rejection reason is required.";
                    return RedirectToAction(nameof(Dashboard));
                }

                var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier).Value);

                var previousStatus = claim.CurrentStatus;
                claim.CurrentStatus = ClaimStatus.Rejected;
                claim.LastModified = DateTime.Now;

                _context.Entry(claim).State = EntityState.Modified;

                var statusHistory = new ClaimStatusHistory
                {
                    ClaimId = claim.ClaimId,
                    ChangedBy = userId,
                    PreviousStatus = previousStatus,
                    NewStatus = ClaimStatus.Rejected,
                    Comments = $"Rejected by Academic Manager: {reason}"
                };

                _context.ClaimStatusHistories.Add(statusHistory);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Claim #CLC-{claim.ClaimId.ToString("D4")} for {claim.User.FirstName} {claim.User.LastName} has been rejected.";
                _logger.LogInformation("Claim {ClaimId} rejected by manager {UserId}. Reason: {Reason}", claim.ClaimId, userId, reason);

                return RedirectToAction(nameof(Dashboard));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting claim {ClaimId}", id);
                TempData["Error"] = "An error occurred while rejecting the claim. Please try again.";
                return RedirectToAction(nameof(Dashboard));
            }
        }

        // Bulk approve claims
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkApprove(List<int> claimIds)
        {
            try
            {
                if (claimIds == null || !claimIds.Any())
                {
                    TempData["Error"] = "No claims selected.";
                    return RedirectToAction(nameof(Dashboard));
                }

                var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier).Value);
                var claims = await _context.Claims
                    .Include(c => c.User)
                    .Where(c => claimIds.Contains(c.ClaimId) && c.CurrentStatus == ClaimStatus.UnderReview)
                    .ToListAsync();

                if (!claims.Any())
                {
                    TempData["Error"] = "No valid claims found for approval.";
                    return RedirectToAction(nameof(Dashboard));
                }

                var approvedCount = 0;
                var totalAmount = 0m;

                foreach (var claim in claims)
                {
                    var previousStatus = claim.CurrentStatus;
                    claim.CurrentStatus = ClaimStatus.Approved;
                    claim.LastModified = DateTime.Now;

                    _context.Entry(claim).State = EntityState.Modified;

                    var statusHistory = new ClaimStatusHistory
                    {
                        ClaimId = claim.ClaimId,
                        ChangedBy = userId,
                        PreviousStatus = previousStatus,
                        NewStatus = ClaimStatus.Approved,
                        Comments = "Bulk approved by Academic Manager"
                    };
                    _context.ClaimStatusHistories.Add(statusHistory);

                    approvedCount++;
                    totalAmount += claim.TotalAmount;
                }

                await _context.SaveChangesAsync();

                TempData["Success"] = $"Successfully approved {approvedCount} claim(s) totaling R{totalAmount:N2}!";
                _logger.LogInformation("{Count} claims bulk approved by manager {UserId}", approvedCount, userId);

                return RedirectToAction(nameof(Dashboard));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error bulk approving claims");
                TempData["Error"] = "An error occurred while approving the claims. Please try again.";
                return RedirectToAction(nameof(Dashboard));
            }
        }

        // Bulk reject claims
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkReject(BulkRejectModel model)
        {
            try
            {
                if (model.ClaimIds == null || !model.ClaimIds.Any())
                {
                    TempData["Error"] = "No claims selected.";
                    return RedirectToAction(nameof(Dashboard));
                }

                if (string.IsNullOrWhiteSpace(model.Reason))
                {
                    TempData["Error"] = "Rejection reason is required.";
                    return RedirectToAction(nameof(Dashboard));
                }

                var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier).Value);
                var claims = await _context.Claims
                    .Include(c => c.User)
                    .Where(c => model.ClaimIds.Contains(c.ClaimId) && c.CurrentStatus == ClaimStatus.UnderReview)
                    .ToListAsync();

                if (!claims.Any())
                {
                    TempData["Error"] = "No valid claims found for rejection.";
                    return RedirectToAction(nameof(Dashboard));
                }

                var rejectedCount = 0;

                foreach (var claim in claims)
                {
                    var previousStatus = claim.CurrentStatus;
                    claim.CurrentStatus = ClaimStatus.Rejected;
                    claim.LastModified = DateTime.Now;

                    _context.Entry(claim).State = EntityState.Modified;

                    var statusHistory = new ClaimStatusHistory
                    {
                        ClaimId = claim.ClaimId,
                        ChangedBy = userId,
                        PreviousStatus = previousStatus,
                        NewStatus = ClaimStatus.Rejected,
                        Comments = $"Bulk rejected by Academic Manager: {model.Reason}"
                    };
                    _context.ClaimStatusHistories.Add(statusHistory);

                    rejectedCount++;
                }

                await _context.SaveChangesAsync();

                TempData["Success"] = $"Successfully rejected {rejectedCount} claim(s).";
                _logger.LogInformation("{Count} claims bulk rejected by manager {UserId}. Reason: {Reason}", rejectedCount, userId, model.Reason);

                return RedirectToAction(nameof(Dashboard));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error bulk rejecting claims");
                TempData["Error"] = "An error occurred while rejecting the claims. Please try again.";
                return RedirectToAction(nameof(Dashboard));
            }
        }

        public async Task<IActionResult> ViewClaim(int id)
        {
            try
            {
                var claim = await _context.Claims
                    .Include(c => c.User)
                    .Include(c => c.Module)
                    .Include(c => c.SupportingDocuments)
                    .Include(c => c.StatusHistory)
                        .ThenInclude(sh => sh.User)
                    .FirstOrDefaultAsync(c => c.ClaimId == id);

                if (claim == null)
                {
                    TempData["Error"] = "Claim not found.";
                    return RedirectToAction(nameof(Dashboard));
                }

                return View(claim);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error viewing claim {ClaimId}", id);
                TempData["Error"] = "An error occurred loading claim details.";
                return RedirectToAction(nameof(Dashboard));
            }
        }

        [HttpGet]
        public async Task<IActionResult> DownloadDocument(int documentId)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier).Value);

                // Get document (manager sees all)
                var document = await _context.SupportingDocuments
                    .Include(d => d.Claim)
                    .FirstOrDefaultAsync(d => d.DocumentId == documentId);

                if (document == null)
                {
                    TempData["Error"] = "Document not found.";
                    return RedirectToAction(nameof(Dashboard));
                }

                var filePath = Path.Combine(_environment.WebRootPath, document.FilePath.TrimStart('/'));

                if (!System.IO.File.Exists(filePath))
                {
                    _logger.LogError("File not found: {FilePath}", filePath);
                    TempData["Error"] = "File not found on server.";
                    return RedirectToAction(nameof(ViewClaim), new { id = document.ClaimId });
                }

                try
                {
                    // Read encrypted file from disk
                    byte[] encryptedData = await System.IO.File.ReadAllBytesAsync(filePath);

                    // Decrypt the file
                    byte[] decryptedData = _encryptionService.DecryptFile(encryptedData);

                    _logger.LogInformation("Document {DocumentId} decrypted and downloaded by manager {UserId}", documentId, userId);

                    // Return decrypted file
                    return File(decryptedData, "application/octet-stream", document.FileName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error decrypting document {DocumentId}", documentId);
                    TempData["Error"] = "Error decrypting file. File may be corrupted.";
                    return RedirectToAction(nameof(ViewClaim), new { id = document.ClaimId });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading document {DocumentId}", documentId);
                TempData["Error"] = "An error occurred downloading the file.";
                return RedirectToAction(nameof(Dashboard));
            }
        }
    }
}
//--------------------------End Of File--------------------------//