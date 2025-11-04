using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SolarConnect.Data;
using SolarConnect.Models;
using System.Linq;
using System.Threading.Tasks;

namespace SolarConnect.Controllers
{
    [Authorize(Roles = "Vendor")]
    public class ProjectController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ProjectController(ApplicationDbContext context)
        {
            _context = context;
        }

        // === VIEW ALL PROJECTS (for Vendor) ===
        public async Task<IActionResult> Index()
        {
            var userEmail = User.Identity.Name;

            var vendor = await _context.Vendors
                .Include(v => v.User)
                .FirstOrDefaultAsync(v => v.User.Email == userEmail);

            if (vendor == null)
                return Unauthorized();

            var projects = await _context.Projects
                .Include(p => p.Quote)
                    .ThenInclude(q => q.Request)
                        .ThenInclude(r => r.Client)
                            .ThenInclude(c => c.User)
                .Where(p => p.Quote.VendorId == vendor.Id)
                .OrderByDescending(p => p.StartDate)
                .ToListAsync();

            return View(projects);
        }

        // === UPDATE PROJECT STATUS (GET) ===
        [HttpGet]
        public async Task<IActionResult> Update(int id)
        {
            var project = await _context.Projects
                .Include(p => p.Quote)
                    .ThenInclude(q => q.Request)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (project == null)
                return NotFound();

            return View(project);
        }

        // === UPDATE PROJECT STATUS (POST) ===
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(Project model)
        {
            var project = await _context.Projects.FindAsync(model.Id);
            if (project == null)
                return NotFound();

            // ✅ Make sure these property names exist in your Project model:
            project.Status = model.Status;
            project.ProgressStage = model.ProgressStage;
            project.MaterialsUsed = model.MaterialsUsed;
            project.InstallationNotes = model.InstallationNotes;

            if (model.Status == "Completed")
                project.CompletionDate = System.DateTime.UtcNow;

            await _context.SaveChangesAsync();

            TempData["Success"] = "✅ Project updated successfully!";
            return RedirectToAction("Index");
        }
    }
}
