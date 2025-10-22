using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Collections.Generic;
using CMCS.Models;
using CMCS.Data;
using CMCS.ViewModels;
using CMCS.Models.CMCS.Models;

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
                var query = _context.Claims
                    .Include(c => c.User)
                    .Include(c => c.Module)
                    .Where(c => c.CurrentStatus == ClaimStatus.Submitted ||
                               c.CurrentStatus == ClaimStatus.UnderReview);

                // Apply filters
                switch (filter.ToLower())
                {
                    case "urgent":
                        query = query.Where(c => (DateTime.Now - c.SubmissionDate).Days > 5);
                        break;
                    case "thisweek":
                        var weekStart = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek);
                        query = query.Where(c => c.SubmissionDate >= weekStart);
                        break;
                    case "bymodule":
                        query = query.OrderBy(c => c.Module.ModuleCode);
                        break;
                    default:
                        query = query.OrderBy(c => c.SubmissionDate);
                        break;
                }

                var viewModel = new CoordinatorDashboardViewModel
                {
                    PendingReview = await _context.Claims
                        .Where(c => c.CurrentStatus == ClaimStatus.Submitted)
                        .CountAsync(),

                    ApprovedToday = await _context.Claims
                        .Where(c => c.CurrentStatus == ClaimStatus.Approved &&
                               c.LastModified.HasValue &&
                               c.LastModified.Value.Date == DateTime.Today)
                        .CountAsync(),

                    UrgentClaims = await _context.Claims
                        .Where(c => c.CurrentStatus == ClaimStatus.Submitted &&
                               (DateTime.Now - c.SubmissionDate).Days > 5)
                        .CountAsync(),

                    TotalThisWeek = await _context.Claims
                        .Where(c => c.CurrentStatus == ClaimStatus.Approved &&
                               c.LastModified.HasValue &&
                               c.LastModified.Value >= DateTime.Today.AddDays(-7))
                        .SumAsync(c => (decimal?)c.TotalAmount) ?? 0,

                    ClaimsForReview = await query.ToListAsync()
                };

                ViewBag.CurrentFilter = filter;
                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading coordinator dashboard");
                TempData["Error"] = "An error occurred loading the dashboard.";
                return View(new CoordinatorDashboardViewModel());
            }
        }

        [HttpPost]
        public async Task<IActionResult> ApproveClaim(int id)
        {
            try
            {
                var claim = await _context.Claims.FindAsync(id);
                if (claim == null)
                {
                    return Json(new { success = false, message = "Claim not found" });
                }

                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

                var previousStatus = claim.CurrentStatus;
                claim.CurrentStatus = ClaimStatus.Approved;
                claim.LastModified = DateTime.Now;

                var statusHistory = new ClaimStatusHistory
                {
                    ClaimId = claim.ClaimId,
                    ChangedBy = userId,
                    PreviousStatus = previousStatus,
                    NewStatus = ClaimStatus.Approved,
                    Comments = "Claim approved by coordinator"
                };

                _context.ClaimStatusHistories.Add(statusHistory);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Claim approved successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving claim {ClaimId}", id);
                return Json(new { success = false, message = "An error occurred approving the claim" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> RejectClaim(int id, string reason)
        {
            try
            {
                var claim = await _context.Claims.FindAsync(id);
                if (claim == null)
                {
                    return Json(new { success = false, message = "Claim not found" });
                }

                if (string.IsNullOrWhiteSpace(reason))
                {
                    return Json(new { success = false, message = "Rejection reason is required" });
                }

                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

                var previousStatus = claim.CurrentStatus;
                claim.CurrentStatus = ClaimStatus.Rejected;
                claim.LastModified = DateTime.Now;

                var statusHistory = new ClaimStatusHistory
                {
                    ClaimId = claim.ClaimId,
                    ChangedBy = userId,
                    PreviousStatus = previousStatus,
                    NewStatus = ClaimStatus.Rejected,
                    Comments = reason
                };

                _context.ClaimStatusHistories.Add(statusHistory);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Claim rejected" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting claim {ClaimId}", id);
                return Json(new { success = false, message = "An error occurred rejecting the claim" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> BulkApprove([FromBody] List<int> claimIds)
        {
            try
            {
                if (claimIds == null || !claimIds.Any())
                {
                    return Json(new { success = false, message = "No claims selected" });
                }

                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
                var claims = await _context.Claims.Where(c => claimIds.Contains(c.ClaimId)).ToListAsync();

                foreach (var claim in claims)
                {
                    var previousStatus = claim.CurrentStatus;
                    claim.CurrentStatus = ClaimStatus.Approved;
                    claim.LastModified = DateTime.Now;

                    var statusHistory = new ClaimStatusHistory
                    {
                        ClaimId = claim.ClaimId,
                        ChangedBy = userId,
                        PreviousStatus = previousStatus,
                        NewStatus = ClaimStatus.Approved,
                        Comments = "Bulk approved by coordinator"
                    };
                    _context.ClaimStatusHistories.Add(statusHistory);
                }

                await _context.SaveChangesAsync();
                return Json(new { success = true, message = $"{claims.Count} claims approved successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error bulk approving claims");
                return Json(new { success = false, message = "An error occurred approving claims" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> BulkReject([FromBody] BulkRejectModel model)
        {
            try
            {
                if (model.ClaimIds == null || !model.ClaimIds.Any())
                {
                    return Json(new { success = false, message = "No claims selected" });
                }

                if (string.IsNullOrWhiteSpace(model.Reason))
                {
                    return Json(new { success = false, message = "Rejection reason is required" });
                }

                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
                var claims = await _context.Claims.Where(c => model.ClaimIds.Contains(c.ClaimId)).ToListAsync();

                foreach (var claim in claims)
                {
                    var previousStatus = claim.CurrentStatus;
                    claim.CurrentStatus = ClaimStatus.Rejected;
                    claim.LastModified = DateTime.Now;

                    var statusHistory = new ClaimStatusHistory
                    {
                        ClaimId = claim.ClaimId,
                        ChangedBy = userId,
                        PreviousStatus = previousStatus,
                        NewStatus = ClaimStatus.Rejected,
                        Comments = model.Reason
                    };
                    _context.ClaimStatusHistories.Add(statusHistory);
                }

                await _context.SaveChangesAsync();
                return Json(new { success = true, message = $"{claims.Count} claims rejected" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error bulk rejecting claims");
                return Json(new { success = false, message = "An error occurred rejecting claims" });
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
    }

    public class BulkRejectModel
    {
        public List<int> ClaimIds { get; set; }
        public string Reason { get; set; }
    }
}