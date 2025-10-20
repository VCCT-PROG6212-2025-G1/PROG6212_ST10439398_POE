using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using CMCS.Models;
using CMCS.Data;
using CMCS.ViewModels;

namespace CMCS.Controllers
{
    [Authorize(Roles = "Coordinator,Manager")]
    public class CoordinatorController : Controller
    {
        private readonly CMCSContext _context;

        public CoordinatorController(CMCSContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Dashboard()
        {
            var today = DateTime.Today;
            var weekStart = today.AddDays(-(int)today.DayOfWeek);

            var viewModel = new CoordinatorDashboardViewModel
            {
                PendingReview = await _context.Claims
                    .Where(c => c.CurrentStatus == ClaimStatus.Submitted ||
                               c.CurrentStatus == ClaimStatus.UnderReview)
                    .CountAsync(),

                ApprovedToday = await _context.Claims
                    .Where(c => c.CurrentStatus == ClaimStatus.Approved &&
                               c.LastModified.HasValue &&
                               c.LastModified.Value.Date == today)
                    .CountAsync(),

                UrgentClaims = await _context.Claims
                    .Where(c => (c.CurrentStatus == ClaimStatus.Submitted ||
                                c.CurrentStatus == ClaimStatus.UnderReview) &&
                               c.SubmissionDate < DateTime.Now.AddDays(-5))
                    .CountAsync(),

                TotalThisWeek = await _context.Claims
                    .Where(c => c.SubmissionDate >= weekStart)
                    .SumAsync(c => c.TotalAmount),

                ClaimsForReview = await _context.Claims
                    .Include(c => c.User)
                    .Include(c => c.Module)
                    .Where(c => c.CurrentStatus == ClaimStatus.Submitted ||
                               c.CurrentStatus == ClaimStatus.UnderReview)
                    .OrderBy(c => c.SubmissionDate)
                    .ToListAsync()
            };

            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> ApproveClaim(int id)
        {
            var claim = await _context.Claims.FindAsync(id);
            if (claim == null)
            {
                return NotFound();
            }

            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            // Update claim status
            var previousStatus = claim.CurrentStatus;
            claim.CurrentStatus = ClaimStatus.Approved;
            claim.LastModified = DateTime.Now;

            // Add status history
            var statusHistory = new ClaimStatusHistory
            {
                ClaimId = claim.ClaimId,
                ChangedBy = userId,
                PreviousStatus = previousStatus,
                NewStatus = ClaimStatus.Rejected,
                Comments = reason ?? "Claim rejected by coordinator"
            };

            _context.ClaimStatusHistories.Add(statusHistory);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Claim rejected.";
            return RedirectToAction(nameof(Dashboard));
        }

        public async Task<IActionResult> ViewClaim(int id)
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
                return NotFound();
            }

            return View(claim);
        }
    }
}
userId,
                PreviousStatus = previousStatus,
                NewStatus = ClaimStatus.Approved,
                Comments = "Claim approved by coordinator"
            };

_context.ClaimStatusHistories.Add(statusHistory);
await _context.SaveChangesAsync();

TempData["Success"] = "Claim approved successfully!";
return RedirectToAction(nameof(Dashboard));
        }

        [HttpPost]
public async Task<IActionResult> RejectClaim(int id, string reason)
{
    var claim = await _context.Claims.FindAsync(id);
    if (claim == null)
    {
        return NotFound();
    }

    var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

    // Update claim status
    var previousStatus = claim.CurrentStatus;
    claim.CurrentStatus = ClaimStatus.Rejected;
    claim.LastModified = DateTime.Now;

    // Add status history
    var statusHistory = new ClaimStatusHistory
    {
        ClaimId = claim.ClaimId,
        ChangedBy =