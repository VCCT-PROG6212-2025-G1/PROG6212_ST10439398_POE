using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.IO;
using Microsoft.AspNetCore.Http;
using CMCS.Models;
using CMCS.Data;
using CMCS.ViewModels;
using Claim = CMCS.Models.Claim;

namespace CMCS.Controllers
{
    [Authorize(Roles = "Lecturer")]
    public class LecturerController : Controller
    {
        private readonly CMCSContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<LecturerController> _logger;

        public LecturerController(CMCSContext context, IWebHostEnvironment environment, ILogger<LecturerController> logger)
        {
            _context = context;
            _environment = environment;
            _logger = logger;
        }

        public async Task<IActionResult> Dashboard()
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

                var viewModel = new LecturerDashboardViewModel
                {
                    PendingClaims = await _context.Claims
                        .Where(c => c.UserId == userId && c.CurrentStatus == ClaimStatus.Submitted)
                        .CountAsync(),

                    MonthlyTotal = await _context.Claims
                        .Where(c => c.UserId == userId &&
                               c.SubmissionDate.Month == DateTime.Now.Month &&
                               c.CurrentStatus == ClaimStatus.Approved)
                        .SumAsync(c => (decimal?)c.TotalAmount) ?? 0,

                    YearToDate = await _context.Claims
                        .Where(c => c.UserId == userId &&
                               c.SubmissionDate.Year == DateTime.Now.Year &&
                               c.CurrentStatus == ClaimStatus.Approved)
                        .SumAsync(c => (decimal?)c.TotalAmount) ?? 0,

                    ClaimsThisMonth = await _context.Claims
                        .Where(c => c.UserId == userId &&
                               c.SubmissionDate.Month == DateTime.Now.Month)
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading lecturer dashboard");
                TempData["Error"] = "An error occurred loading the dashboard. Please try again.";
                return View(new LecturerDashboardViewModel());
            }
        }

        [HttpGet]
        public async Task<IActionResult> SubmitClaim()
        {
            try
            {
                var modules = await _context.Modules
                    .Where(m => m.IsActive)
                    .OrderBy(m => m.ModuleCode)
                    .ToListAsync();

                var viewModel = new ClaimSubmissionViewModel
                {
                    AvailableModules = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(
                        modules, "ModuleId", "ModuleName")
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading claim submission form");
                TempData["Error"] = "An error occurred loading the form. Please try again.";
                return RedirectToAction(nameof(Dashboard));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitClaim(ClaimSubmissionViewModel model, bool saveAsDraft = false)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var modules = await _context.Modules.Where(m => m.IsActive).ToListAsync();
                    model.AvailableModules = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(
                        modules, "ModuleId", "ModuleName");
                    TempData["Error"] = "Please correct the errors in the form.";
                    return View(model);
                }

                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
                var module = await _context.Modules.FindAsync(model.ModuleId);

                if (module == null)
                {
                    TempData["Error"] = "Invalid module selected.";
                    return RedirectToAction(nameof(SubmitClaim));
                }

                var claim = new Claim
                {
                    UserId = userId,
                    ModuleId = model.ModuleId,
                    HoursWorked = model.HoursWorked,
                    HourlyRate = module.StandardHourlyRate,
                    TotalAmount = model.HoursWorked * module.StandardHourlyRate,
                    ClaimPeriod = model.ClaimPeriod,
                    AdditionalNotes = model.AdditionalNotes,
                    CurrentStatus = saveAsDraft ? ClaimStatus.Draft : ClaimStatus.Submitted,
                    SubmissionDate = DateTime.Now
                };

                _context.Claims.Add(claim);
                await _context.SaveChangesAsync();

                // Handle file uploads
                if (model.Documents != null && model.Documents.Any())
                {
                    var uploadResult = await HandleFileUploads(model.Documents, claim.ClaimId);
                    if (!uploadResult.Success)
                    {
                        TempData["Warning"] = $"Claim submitted but some files failed to upload: {uploadResult.Message}";
                        return RedirectToAction(nameof(Dashboard));
                    }
                }

                // Add status history
                var statusHistory = new ClaimStatusHistory
                {
                    ClaimId = claim.ClaimId,
                    ChangedBy = userId,
                    PreviousStatus = ClaimStatus.Draft,
                    NewStatus = claim.CurrentStatus,
                    Comments = saveAsDraft ? "Claim saved as draft" : "Claim submitted by lecturer"
                };

                _context.ClaimStatusHistories.Add(statusHistory);
                await _context.SaveChangesAsync();

                TempData["Success"] = saveAsDraft ? "Claim saved as draft!" : "Claim submitted successfully!";
                return RedirectToAction(nameof(Dashboard));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting claim");
                TempData["Error"] = "An error occurred submitting your claim. Please try again.";
                return RedirectToAction(nameof(SubmitClaim));
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetHourlyRate(int moduleId)
        {
            try
            {
                var module = await _context.Modules.FindAsync(moduleId);
                if (module == null)
                {
                    return NotFound(new { error = "Module not found" });
                }

                return Json(new { hourlyRate = module.StandardHourlyRate });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching hourly rate for module {ModuleId}", moduleId);
                return BadRequest(new { error = "Error fetching hourly rate" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> ViewClaim(int id)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

                var claim = await _context.Claims
                    .Include(c => c.Module)
                    .Include(c => c.User)
                    .Include(c => c.SupportingDocuments)
                    .Include(c => c.StatusHistory)
                        .ThenInclude(sh => sh.User)
                    .FirstOrDefaultAsync(c => c.ClaimId == id && c.UserId == userId);

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
                TempData["Error"] = "An error occurred loading the claim details.";
                return RedirectToAction(nameof(Dashboard));
            }
        }

        [HttpGet]
        public async Task<IActionResult> ClaimHistory()
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

                var claims = await _context.Claims
                    .Include(c => c.Module)
                    .Where(c => c.UserId == userId)
                    .OrderByDescending(c => c.SubmissionDate)
                    .ToListAsync();

                return View(claims);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading claim history");
                TempData["Error"] = "An error occurred loading your claim history.";
                return RedirectToAction(nameof(Dashboard));
            }
        }

        private async Task<(bool Success, string Message)> HandleFileUploads(List<IFormFile> files, int claimId)
        {
            try
            {
                var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads");
                Directory.CreateDirectory(uploadsFolder);

                var allowedExtensions = new[] { ".pdf", ".doc", ".docx", ".xls", ".xlsx" };
                var maxFileSize = 10 * 1024 * 1024; // 10MB

                foreach (var file in files)
                {
                    if (file.Length == 0)
                        continue;

                    // Validate file extension
                    var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                    if (!allowedExtensions.Contains(extension))
                    {
                        return (false, $"File type {extension} is not allowed. Only PDF, DOC, DOCX, XLS, XLSX are permitted.");
                    }

                    // Validate file size
                    if (file.Length > maxFileSize)
                    {
                        return (false, $"File {file.FileName} exceeds maximum size of 10MB.");
                    }

                    var fileName = $"{claimId}_{Guid.NewGuid()}{extension}";
                    var filePath = Path.Combine(uploadsFolder, fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }

                    var document = new SupportingDocument
                    {
                        ClaimId = claimId,
                        FileName = file.FileName,
                        FilePath = filePath,
                        FileSize = file.Length,
                        FileType = extension
                    };

                    _context.SupportingDocuments.Add(document);
                }

                await _context.SaveChangesAsync();
                return (true, "All files uploaded successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading files for claim {ClaimId}", claimId);
                return (false, "An error occurred uploading files");
            }
        }
    }
}