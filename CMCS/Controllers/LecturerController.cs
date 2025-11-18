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
    [Authorize(Roles = "Lecturer")]
    public class LecturerController : Controller
    {
        private readonly CMCSContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<LecturerController> _logger;
        private readonly IFileEncryptionService _encryptionService;

        public LecturerController(CMCSContext context, IWebHostEnvironment environment, ILogger<LecturerController> logger, IFileEncryptionService encryptionService)
        {
            _context = context;
            _environment = environment;
            _logger = logger;
            _encryptionService = encryptionService;
        }

        public async Task<IActionResult> Dashboard()
        {
            try
            {
                var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier).Value);

                var viewModel = new LecturerDashboardViewModel
                {
                    PendingClaims = await _context.Claims
                        .Where(c => c.UserId == userId &&
                               (c.CurrentStatus == ClaimStatus.Submitted || c.CurrentStatus == ClaimStatus.UnderReview))
                        .CountAsync(),

                    MonthlyTotal = await _context.Claims
                        .Where(c => c.UserId == userId &&
                               c.SubmissionDate.Month == DateTime.Now.Month &&
                               c.SubmissionDate.Year == DateTime.Now.Year &&
                               c.CurrentStatus == ClaimStatus.Approved)
                        .SumAsync(c => (decimal?)c.TotalAmount) ?? 0,

                    YearToDate = await _context.Claims
                        .Where(c => c.UserId == userId &&
                               c.SubmissionDate.Year == DateTime.Now.Year &&
                               c.CurrentStatus == ClaimStatus.Approved)
                        .SumAsync(c => (decimal?)c.TotalAmount) ?? 0,

                    ClaimsThisMonth = await _context.Claims
                        .Where(c => c.UserId == userId &&
                               c.SubmissionDate.Month == DateTime.Now.Month &&
                               c.SubmissionDate.Year == DateTime.Now.Year)
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

                ViewBag.Modules = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(
                    modules, "ModuleId", "ModuleName");

                return View(new ClaimSubmissionViewModel());
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
        public async Task<IActionResult> SubmitClaim(ClaimSubmissionViewModel model, List<IFormFile> SupportingDocuments)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var modules = await _context.Modules.Where(m => m.IsActive).ToListAsync();
                    ViewBag.Modules = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(
                        modules, "ModuleId", "ModuleName");
                    TempData["Error"] = "Please correct the errors in the form.";
                    return View(model);
                }

                var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier).Value);
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
                    CurrentStatus = ClaimStatus.Submitted,
                    SubmissionDate = DateTime.Now
                };

                _context.Claims.Add(claim);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Claim {ClaimId} created for user {UserId}", claim.ClaimId, userId);

                // Handle file uploads AFTER claim is saved
                if (SupportingDocuments != null && SupportingDocuments.Any())
                {
                    var uploadResult = await HandleFileUploads(SupportingDocuments, claim.ClaimId);
                    if (!uploadResult.Success)
                    {
                        _logger.LogWarning("File upload failed for claim {ClaimId}: {Message}", claim.ClaimId, uploadResult.Message);
                        TempData["Warning"] = $"Claim submitted but some files failed to upload: {uploadResult.Message}";
                    }
                    else
                    {
                        _logger.LogInformation("Files uploaded successfully for claim {ClaimId}", claim.ClaimId);
                    }
                }

                // Add status history
                var statusHistory = new ClaimStatusHistory
                {
                    ClaimId = claim.ClaimId,
                    ChangedBy = userId,
                    PreviousStatus = ClaimStatus.Draft,
                    NewStatus = ClaimStatus.Submitted,
                    Comments = "Claim submitted by lecturer"
                };

                _context.ClaimStatusHistories.Add(statusHistory);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Claim submitted successfully!";
                return RedirectToAction(nameof(Dashboard));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting claim");
                TempData["Error"] = "An error occurred submitting your claim. Please try again.";

                var modules = await _context.Modules.Where(m => m.IsActive).ToListAsync();
                ViewBag.Modules = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(
                    modules, "ModuleId", "ModuleName");
                return View(model);
            }
        }

        public async Task<IActionResult> ViewClaim(int id)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier).Value);

                var claim = await _context.Claims
                    .Include(c => c.Module)
                    .Include(c => c.User)
                    .Include(c => c.StatusHistory.OrderBy(sh => sh.ChangeDate))
                    .Include(c => c.SupportingDocuments)
                    .FirstOrDefaultAsync(c => c.ClaimId == id && c.UserId == userId);

                if (claim == null)
                {
                    TempData["Error"] = "Claim not found.";
                    return RedirectToAction(nameof(ClaimHistory));
                }

                return View(claim);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error viewing claim {ClaimId}", id);
                TempData["Error"] = "Error loading claim details.";
                return RedirectToAction(nameof(ClaimHistory));
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
        public async Task<IActionResult> ClaimHistory()
        {
            try
            {
                var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier).Value);

                var claims = await _context.Claims
                    .Include(c => c.Module)
                    .Include(c => c.StatusHistory)
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
                if (files == null || !files.Any())
                {
                    return (true, "No files to upload");
                }

                var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads");

                // Ensure directory exists
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                    _logger.LogInformation("Created uploads directory: {Path}", uploadsFolder);
                }

                var allowedExtensions = new[] { ".pdf", ".doc", ".docx", ".xls", ".xlsx" };
                var maxFileSize = 10 * 1024 * 1024; // 10MB
                var uploadedCount = 0;

                foreach (var file in files)
                {
                    if (file == null || file.Length == 0)
                    {
                        _logger.LogWarning("Skipping empty file");
                        continue;
                    }

                    var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

                    if (string.IsNullOrEmpty(extension))
                    {
                        _logger.LogWarning("File {FileName} has no extension", file.FileName);
                        continue;
                    }

                    if (!allowedExtensions.Contains(extension))
                    {
                        return (false, $"File type {extension} is not allowed. Only PDF, DOC, DOCX, XLS, XLSX are supported.");
                    }

                    if (file.Length > maxFileSize)
                    {
                        return (false, $"File {file.FileName} exceeds maximum size of 10MB.");
                    }

                    try
                    {
                        // Encrypt the file
                        byte[] encryptedData = await _encryptionService.EncryptFileAsync(file);

                        // Generate unique filename for encrypted file (always .enc)
                        var uniqueFileName = $"{claimId}_{Guid.NewGuid()}.enc";
                        var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                        // Save encrypted file to disk
                        await System.IO.File.WriteAllBytesAsync(filePath, encryptedData);

                        // Verify file was saved
                        if (!System.IO.File.Exists(filePath))
                        {
                            _logger.LogError("File was not saved to disk: {FilePath}", filePath);
                            return (false, "Failed to save file to disk");
                        }

                        var fileInfo = new FileInfo(filePath);
                        _logger.LogInformation("Encrypted file saved to disk: {FileName} ({Size} bytes)", uniqueFileName, fileInfo.Length);

                        // Create database record with original filename and extension
                        var document = new SupportingDocument
                        {
                            ClaimId = claimId,
                            FileName = file.FileName,
                            FilePath = $"/uploads/{uniqueFileName}",
                            FileSize = file.Length, // Store original file size
                            FileType = extension,
                            Description = $"Uploaded document: {file.FileName}"
                        };

                        _context.SupportingDocuments.Add(document);
                        uploadedCount++;

                        _logger.LogInformation("Encrypted document record created for: {FileName} -> {FilePath}", file.FileName, document.FilePath);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error encrypting and saving file {FileName}", file.FileName);
                        return (false, $"Error saving file {file.FileName}: {ex.Message}");
                    }
                }

                // Save all document records to database
                if (uploadedCount > 0)
                {
                    var savedCount = await _context.SaveChangesAsync();
                    _logger.LogInformation("Saved {Count} encrypted document records to database for claim {ClaimId}", savedCount, claimId);

                    if (savedCount == 0)
                    {
                        _logger.LogError("SaveChanges returned 0 for {Count} documents", uploadedCount);
                        return (false, "Documents uploaded but failed to save to database");
                    }
                }

                return (true, $"Successfully uploaded and encrypted {uploadedCount} file(s)");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in HandleFileUploads for claim {ClaimId}", claimId);
                return (false, $"An error occurred uploading files: {ex.Message}");
            }
        }

        [HttpGet]
        public async Task<IActionResult> DownloadDocument(int documentId)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier).Value);

                // Get document and verify ownership
                var document = await _context.SupportingDocuments
                    .Include(d => d.Claim)
                    .FirstOrDefaultAsync(d => d.DocumentId == documentId && d.Claim.UserId == userId);

                if (document == null)
                {
                    TempData["Error"] = "Document not found or access denied.";
                    return RedirectToAction(nameof(ClaimHistory));
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

                    _logger.LogInformation("Document {DocumentId} decrypted and downloaded by user {UserId}", documentId, userId);

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
                return RedirectToAction(nameof(ClaimHistory));
            }
        }
    }
}
//--------------------------End Of File--------------------------////--------------------------End Of File--------------------------//