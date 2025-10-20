using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.IO;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using CMCS.Models;
using CMCS.Data;
using CMCS.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;

namespace CMCS.Controllers
{
    [Authorize(Roles = "Lecturer")]
    public class LecturerController : Controller
    {
        private readonly CMCSContext _context;
        private readonly IWebHostEnvironment _environment;

        public LecturerController(CMCSContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        public async Task<IActionResult> Dashboard()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
            var currentMonth = DateTime.Now.ToString("yyyy-MM");

            var viewModel = new LecturerDashboardViewModel
            {
                PendingClaims = await _context.Claims
                    .Where(c => c.UserId == userId &&
                           (c.CurrentStatus == ClaimStatus.Submitted ||
                            c.CurrentStatus == ClaimStatus.UnderReview))
                    .CountAsync(),

                MonthlyTotal = await _context.Claims
                    .Where(c => c.UserId == userId &&
                           c.ClaimPeriod == currentMonth)
                    .SumAsync(c => c.TotalAmount),

                YearToDate = await _context.Claims
                    .Where(c => c.UserId == userId &&
                           c.SubmissionDate.Year == DateTime.Now.Year)
                    .SumAsync(c => c.TotalAmount),

                ClaimsThisMonth = await _context.Claims
                    .Where(c => c.UserId == userId &&
                           c.ClaimPeriod == currentMonth)
                    .CountAsync(),

                RecentClaims = await _context.Claims
                    .Include(c => c.Module)
                    .Where(c => c.UserId == userId)
                    .OrderByDescending(c => c.SubmissionDate)
                    .Take(5)
                    .ToListAsync()
            };

            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> SubmitClaim()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            // Get modules assigned to this lecturer
            var modules = await _context.Modules
                .Where(m => m.IsActive)
                .ToListAsync();

            var viewModel = new ClaimSubmissionViewModel
            {
                ClaimPeriod = DateTime.Now.ToString("yyyy-MM"),
                AvailableModules = new SelectList(modules, "ModuleId", "ModuleName")
            };

            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> SubmitClaim(ClaimSubmissionViewModel model)
        {
            if (ModelState.IsValid)
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
                var module = await _context.Modules.FindAsync(model.ModuleId);

                var claim = new Claim
                {
                    UserId = userId,
                    ModuleId = model.ModuleId,
                    HoursWorked = model.HoursWorked,
                    HourlyRate = module.StandardHourlyRate,
                    TotalAmount = model.HoursWorked * module.StandardHourlyRate,
                    ClaimPeriod = model.ClaimPeriod,
                    AdditionalNotes = model.AdditionalNotes,
                    CurrentStatus = ClaimStatus.Submitted,
                    SubmissionDate = DateTime.Now
                };

                _context.Claims.Add(claim);
                await _context.SaveChangesAsync();

                // Handle file uploads
                if (model.Documents != null && model.Documents.Count > 0)
                {
                    foreach (var file in model.Documents)
                    {
                        if (file.Length > 0 && file.Length <= 10485760) // 10MB limit
                        {
                            var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads");
                            if (!Directory.Exists(uploadsFolder))
                            {
                                Directory.CreateDirectory(uploadsFolder);
                            }

                            var uniqueFileName = Guid.NewGuid().ToString() + "_" + file.FileName;
                            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                            using (var fileStream = new FileStream(filePath, FileMode.Create))
                            {
                                await file.CopyToAsync(fileStream);
                            }

                            var document = new SupportingDocument
                            {
                                ClaimId = claim.ClaimId,
                                FileName = file.FileName,
                                FilePath = uniqueFileName,
                                FileSize = file.Length,
                                FileType = Path.GetExtension(file.FileName)
                            };

                            _context.SupportingDocuments.Add(document);
                        }
                    }
                    await _context.SaveChangesAsync();
                }

                // Add status history
                var statusHistory = new ClaimStatusHistory
                {
                    ClaimId = claim.ClaimId,
                    ChangedBy = userId,
                    PreviousStatus = ClaimStatus.Draft,
                    NewStatus = ClaimStatus.Submitted,
                    Comments = "Claim submitted"
                };

                _context.ClaimStatusHistories.Add(statusHistory);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Claim submitted successfully!";
                return RedirectToAction(nameof(Dashboard));
            }

            // Reload modules if model is invalid
            var modules = await _context.Modules.Where(m => m.IsActive).ToListAsync();
            model.AvailableModules = new SelectList(modules, "ModuleId", "ModuleName");

            return View(model);
        }

        public async Task<IActionResult> ClaimHistory()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            var claims = await _context.Claims
                .Include(c => c.Module)
                .Where(c => c.UserId == userId)
                .OrderByDescending(c => c.SubmissionDate)
                .ToListAsync();

            return View(claims);
        }
    }
}