//--------------------------Start Of File--------------------------//
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using CMCS.Models;
using CMCS.Data;
using CMCS.ViewModels;

namespace CMCS.Controllers
{
    [Authorize(Roles = "Coordinator,Manager")]
    public class CoordinatorController : Controller
    {
        private readonly CMCSContext _context;
        private readonly ILogger<CoordinatorController> _logger;

        public CoordinatorController(CMCSContext context, ILogger<CoordinatorController> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IActionResult> Dashboard(string filter = "all")
        {
            try
            {
                var today = DateTime.Today;
                var startOfWeek = today.AddDays(-(int)today.DayOfWeek);

                var viewModel = new CoordinatorDashboardViewModel
                {
                    PendingReview = 0,
                    ApprovedToday = 0,
                    UrgentClaims = 0,
                    TotalThisWeek = 0,
                    ClaimsForReview = new List<Claim>()
                };

                try
                {
                    viewModel.PendingReview = await _context.Claims
                        .Where(c => c.CurrentStatus == ClaimStatus.Submitted || c.CurrentStatus == ClaimStatus.UnderReview)
                        .CountAsync();

                    viewModel.ApprovedToday = await _context.Claims
                        .Include(c => c.StatusHistory)
                        .Where(c => c.CurrentStatus == ClaimStatus.Approved &&
                                   c.StatusHistory.Any(sh => sh.NewStatus == ClaimStatus.Approved &&
                                                            sh.ChangeDate.Date == today))
                        .CountAsync();

                    var allPendingClaims = await _context.Claims
                        .Where(c => (c.CurrentStatus == ClaimStatus.Submitted || c.CurrentStatus == ClaimStatus.UnderReview))
                        .ToListAsync();
                    viewModel.UrgentClaims = allPendingClaims.Count(c => (DateTime.Now - c.SubmissionDate).Days > 5);

                    viewModel.TotalThisWeek = await _context.Claims
                        .Include(c => c.StatusHistory)
                        .Where(c => c.CurrentStatus == ClaimStatus.Approved &&
                                   c.StatusHistory.Any(sh => sh.NewStatus == ClaimStatus.Approved &&
                                                            sh.ChangeDate >= startOfWeek &&
                                                            sh.ChangeDate <= DateTime.Now))
                        .SumAsync(c => (decimal?)c.TotalAmount) ?? 0;

                    var query = _context.Claims
                        .Include(c => c.User)
                        .Include(c => c.Module)
                        .Include(c => c.StatusHistory)
                        .Where(c => c.CurrentStatus == ClaimStatus.Submitted || c.CurrentStatus == ClaimStatus.UnderReview);

                    switch (filter.ToLower())
                    {
                        case "urgent":
                            var allClaims = await query.ToListAsync();
                            viewModel.ClaimsForReview = allClaims
                                .Where(c => (DateTime.Now - c.SubmissionDate).Days > 5)
                                .OrderByDescending(c => c.SubmissionDate)
                                .ToList();
                            break;

                        case "thisweek":
                            viewModel.ClaimsForReview = await query
                                .Where(c => c.SubmissionDate >= startOfWeek)
                                .OrderByDescending(c => c.SubmissionDate)
                                .ToListAsync();
                            break;

                        case "bymodule":
                            viewModel.ClaimsForReview = await query
                                .OrderBy(c => c.Module.ModuleCode)
                                .ThenByDescending(c => c.SubmissionDate)
                                .ToListAsync();
                            break;

                        default:
                            viewModel.ClaimsForReview = await query
                                .OrderByDescending(c => c.SubmissionDate)
                                .ToListAsync();
                            break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error fetching dashboard data");
                    TempData["Warning"] = "Some dashboard data could not be loaded.";
                }

                ViewBag.CurrentFilter = filter;
                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical error loading coordinator dashboard");
                TempData["Error"] = "An error occurred loading the dashboard. Please try again.";
                return View(new CoordinatorDashboardViewModel
                {
                    ClaimsForReview = new List<Claim>()
                });
            }
        }

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
                    Comments = $"Claim approved by {(User.IsInRole("Manager") ? "Academic Manager" : "Programme Coordinator")}"
                };

                _context.ClaimStatusHistories.Add(statusHistory);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Claim #CLC-{claim.ClaimId.ToString("D4")} for {claim.User.FirstName} {claim.User.LastName} has been approved successfully! Amount: R{claim.TotalAmount:N2}";
                _logger.LogInformation("Claim {ClaimId} approved by user {UserId}", claim.ClaimId, userId);

                return RedirectToAction(nameof(Dashboard));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving claim {ClaimId}", id);
                TempData["Error"] = "An error occurred while approving the claim. Please try again.";
                return RedirectToAction(nameof(Dashboard));
            }
        }

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
                    Comments = $"Rejected by {(User.IsInRole("Manager") ? "Academic Manager" : "Programme Coordinator")}: {reason}"
                };

                _context.ClaimStatusHistories.Add(statusHistory);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Claim #CLC-{claim.ClaimId.ToString("D4")} for {claim.User.FirstName} {claim.User.LastName} has been rejected.";
                _logger.LogInformation("Claim {ClaimId} rejected by user {UserId}. Reason: {Reason}", claim.ClaimId, userId, reason);

                return RedirectToAction(nameof(Dashboard));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting claim {ClaimId}", id);
                TempData["Error"] = "An error occurred while rejecting the claim. Please try again.";
                return RedirectToAction(nameof(Dashboard));
            }
        }

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
                    .Where(c => claimIds.Contains(c.ClaimId))
                    .ToListAsync();

                if (!claims.Any())
                {
                    TempData["Error"] = "No valid claims found.";
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
                        Comments = $"Bulk approved by {(User.IsInRole("Manager") ? "Academic Manager" : "Programme Coordinator")}"
                    };
                    _context.ClaimStatusHistories.Add(statusHistory);

                    approvedCount++;
                    totalAmount += claim.TotalAmount;
                }

                await _context.SaveChangesAsync();

                TempData["Success"] = $"Successfully approved {approvedCount} claim(s) totaling R{totalAmount:N2}!";
                _logger.LogInformation("{Count} claims bulk approved by user {UserId}", approvedCount, userId);

                return RedirectToAction(nameof(Dashboard));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error bulk approving claims");
                TempData["Error"] = "An error occurred while approving the claims. Please try again.";
                return RedirectToAction(nameof(Dashboard));
            }
        }

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
                    .Where(c => model.ClaimIds.Contains(c.ClaimId))
                    .ToListAsync();

                if (!claims.Any())
                {
                    TempData["Error"] = "No valid claims found.";
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
                        Comments = $"Bulk rejected by {(User.IsInRole("Manager") ? "Academic Manager" : "Programme Coordinator")}: {model.Reason}"
                    };
                    _context.ClaimStatusHistories.Add(statusHistory);

                    rejectedCount++;
                }

                await _context.SaveChangesAsync();

                TempData["Success"] = $"Successfully rejected {rejectedCount} claim(s).";
                _logger.LogInformation("{Count} claims bulk rejected by user {UserId}. Reason: {Reason}", rejectedCount, userId, model.Reason);

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
    }

    public class BulkRejectModel
    {
        public List<int> ClaimIds { get; set; }
        public string Reason { get; set; }
    }
}
//--------------------------End Of File--------------------------//