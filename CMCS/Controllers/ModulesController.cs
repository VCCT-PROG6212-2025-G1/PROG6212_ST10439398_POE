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

        public ModulesController(CMCSContext context)
        {
            _context = context;
        }

        // GET: Modules
        public async Task<IActionResult> Index()
        {
            var modules = await _context.Modules
                .OrderBy(m => m.ModuleCode)
                .ToListAsync();
            return View(modules);
        }

        // GET: Modules/Details/5
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

        // GET: Modules/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Modules/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ModuleCode,ModuleName,StandardHourlyRate,Description,IsActive")] Module module)
        {
            if (ModelState.IsValid)
            {
                // Check if module code already exists
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

        // GET: Modules/Edit/5
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

        // POST: Modules/Edit/5
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
                    // Check if module code already exists for a different module
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

        // GET: Modules/Delete/5
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

        // POST: Modules/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var module = await _context.Modules
                .Include(m => m.Claims)
                .FirstOrDefaultAsync(m => m.ModuleId == id);

            if (module == null)
            {
                return NotFound();
            }

            // Check if module has associated claims
            if (module.Claims != null && module.Claims.Any())
            {
                TempData["Error"] = "Cannot delete module with associated claims. Please deactivate it instead.";
                return RedirectToAction(nameof(Index));
            }

            _context.Modules.Remove(module);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Module deleted successfully!";
            return RedirectToAction(nameof(Index));
        }

        // POST: Modules/ToggleActive/5
        [HttpPost]
        public async Task<IActionResult> ToggleActive(int id)
        {
            var module = await _context.Modules.FindAsync(id);
            if (module == null)
            {
                return NotFound();
            }

            module.IsActive = !module.IsActive;
            module.LastModified = DateTime.Now;
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Module {(module.IsActive ? "activated" : "deactivated")} successfully!";
            return RedirectToAction(nameof(Index));
        }

        private bool ModuleExists(int id)
        {
            return _context.Modules.Any(e => e.ModuleId == id);
        }
    }
}