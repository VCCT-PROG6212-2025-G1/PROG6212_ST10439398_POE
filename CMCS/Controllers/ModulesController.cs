//--------------------------Start Of File--------------------------//
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using CMCS.Models;
using CMCS.Data;

namespace CMCS.Controllers
{
    [Authorize(Roles = "Coordinator,Manager")]
    public class ModulesController : Controller
    {
        private readonly CMCSContext _context;
        private readonly ILogger<ModulesController> _logger;

        public ModulesController(CMCSContext context, ILogger<ModulesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var modules = await _context.Modules
                .OrderBy(m => m.ModuleCode)
                .ToListAsync();
            return View(modules);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var module = await _context.Modules
                .Include(m => m.Claims)
                .FirstOrDefaultAsync(m => m.ModuleId == id);

            if (module == null)
            {
                return NotFound();
            }

            return View(module);
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ModuleCode,ModuleName,StandardHourlyRate,Description,IsActive")] Module module)
        {
            if (ModelState.IsValid)
            {
                var existingModule = await _context.Modules
                    .FirstOrDefaultAsync(m => m.ModuleCode == module.ModuleCode);

                if (existingModule != null)
                {
                    ModelState.AddModelError("ModuleCode", "A module with this code already exists.");
                    return View(module);
                }

                module.CreatedDate = DateTime.Now;
                _context.Add(module);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Module created successfully!";
                return RedirectToAction(nameof(Index));
            }
            return View(module);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var module = await _context.Modules.FindAsync(id);
            if (module == null)
            {
                return NotFound();
            }
            return View(module);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("ModuleId,ModuleCode,ModuleName,StandardHourlyRate,Description,IsActive,CreatedDate")] Module module)
        {
            if (id != module.ModuleId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var existingModule = await _context.Modules
                        .FirstOrDefaultAsync(m => m.ModuleCode == module.ModuleCode && m.ModuleId != id);

                    if (existingModule != null)
                    {
                        ModelState.AddModelError("ModuleCode", "A module with this code already exists.");
                        return View(module);
                    }

                    module.LastModified = DateTime.Now;
                    _context.Update(module);
                    await _context.SaveChangesAsync();

                    TempData["Success"] = "Module updated successfully!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ModuleExists(module.ModuleId))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(module);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var module = await _context.Modules
                .Include(m => m.Claims)
                .FirstOrDefaultAsync(m => m.ModuleId == id);

            if (module == null)
            {
                return NotFound();
            }

            return View(module);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var module = await _context.Modules
                    .Include(m => m.Claims)
                    .FirstOrDefaultAsync(m => m.ModuleId == id);

                if (module == null)
                {
                    TempData["Error"] = "Module not found.";
                    return RedirectToAction(nameof(Index));
                }

                if (module.Claims != null && module.Claims.Any())
                {
                    module.IsActive = false;
                    module.LastModified = DateTime.Now;

                    _context.Entry(module).State = EntityState.Modified;
                    await _context.SaveChangesAsync();

                    TempData["Warning"] = $"Module '{module.ModuleCode}' has {module.Claims.Count} associated claim(s) and has been deactivated instead of deleted. To permanently delete, remove all claims first.";
                    _logger.LogInformation("Module {ModuleId} deactivated due to existing claims", id);

                    return RedirectToAction(nameof(Index));
                }

                _context.Modules.Remove(module);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Module '{module.ModuleCode}' deleted successfully!";
                _logger.LogInformation("Module {ModuleId} deleted", id);

                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error deleting module {ModuleId}", id);
                TempData["Error"] = "Cannot delete module due to database constraints. The module has been deactivated instead.";

                var module = await _context.Modules.FindAsync(id);
                if (module != null)
                {
                    module.IsActive = false;
                    module.LastModified = DateTime.Now;
                    _context.Entry(module).State = EntityState.Modified;
                    await _context.SaveChangesAsync();
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting module {ModuleId}", id);
                TempData["Error"] = "An error occurred while deleting the module.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(int id)
        {
            try
            {
                var module = await _context.Modules.FindAsync(id);
                if (module == null)
                {
                    TempData["Error"] = "Module not found.";
                    return RedirectToAction(nameof(Index));
                }

                module.IsActive = !module.IsActive;
                module.LastModified = DateTime.Now;

                _context.Entry(module).State = EntityState.Modified;
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Module '{module.ModuleCode}' {(module.IsActive ? "activated" : "deactivated")} successfully!";
                _logger.LogInformation("Module {ModuleId} {Action}", id, module.IsActive ? "activated" : "deactivated");

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling module {ModuleId} active status", id);
                TempData["Error"] = "An error occurred while updating the module status.";
                return RedirectToAction(nameof(Index));
            }
        }

        private bool ModuleExists(int id)
        {
            return _context.Modules.Any(e => e.ModuleId == id);
        }
    }
}
//--------------------------End Of File--------------------------//