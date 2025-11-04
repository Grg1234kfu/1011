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
    public class VendorController : Controller
    {
        private readonly ApplicationDbContext _context;

        public VendorController(ApplicationDbContext context)
        {
            _context = context;
        }

        // === DASHBOARD ===
        public async Task<IActionResult> Dashboard()
        {
            var userEmail = User.Identity.Name;

            var vendor = await _context.Vendors
                .Include(v => v.User)
                .FirstOrDefaultAsync(v => v.User.Email == userEmail);

            if (vendor == null)
                return Unauthorized();

            // ✅ Updated Stats (Dashboard Counters)
            ViewBag.AvailableRequests = await _context.Requests.CountAsync(r => r.Status == "Open");
            ViewBag.MyQuotes = await _context.Quotes.CountAsync(q => q.VendorId == vendor.Id);
            ViewBag.AcceptedProjects = await _context.Projects
                .Include(p => p.Quote)
                .CountAsync(p => p.Quote.VendorId == vendor.Id && p.Status == "Completed");
            ViewBag.MyProjects = await _context.Projects
                .Include(p => p.Quote)
                .CountAsync(p => p.Quote.VendorId == vendor.Id); // ✅ new dashboard stat

            return View(vendor);
        }

        // === VIEW ALL CLIENT REQUESTS ===
        public async Task<IActionResult> ClientRequests()
        {
            var requests = await _context.Requests
                .Include(r => r.Client)
                    .ThenInclude(c => c.User)
                .Where(r => r.Status == "Open")
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            return View(requests);
        }

        // === CREATE QUOTE (GET) ===
        [HttpGet]
        public async Task<IActionResult> CreateQuote(int requestId)
        {
            var request = await _context.Requests
                .Include(r => r.Client)
                    .ThenInclude(c => c.User)
                .FirstOrDefaultAsync(r => r.Id == requestId);

            if (request == null)
                return NotFound();

            ViewBag.RequestId = requestId;
            ViewBag.ClientName = $"{request.Client.User.FirstName} {request.Client.User.LastName}";

            return View();
        }

        // === CREATE QUOTE (POST) ===
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateQuote(Quote model)
        {
            var userEmail = User.Identity.Name;
            var vendor = await _context.Vendors
                .Include(v => v.User)
                .FirstOrDefaultAsync(v => v.User.Email == userEmail);

            if (vendor == null)
                return Unauthorized();

            if (ModelState.IsValid)
            {
                model.VendorId = vendor.Id;
                model.SubmittedAt = System.DateTime.UtcNow;
                model.Status = "Pending";

                _context.Quotes.Add(model);
                await _context.SaveChangesAsync();

                TempData["Success"] = "✅ Your quote has been submitted successfully!";
                return RedirectToAction("MyQuotes");
            }

            var request = await _context.Requests
                .Include(r => r.Client)
                    .ThenInclude(c => c.User)
                .FirstOrDefaultAsync(r => r.Id == model.RequestId);

            if (request != null)
            {
                ViewBag.RequestId = model.RequestId;
                ViewBag.ClientName = $"{request.Client.User.FirstName} {request.Client.User.LastName}";
            }

            return View(model);
        }

        // === MANAGE MY QUOTES ===
        public async Task<IActionResult> MyQuotes()
        {
            var userEmail = User.Identity.Name;
            var vendor = await _context.Vendors
                .Include(v => v.User)
                .FirstOrDefaultAsync(v => v.User.Email == userEmail);

            if (vendor == null)
                return Unauthorized();

            var quotes = await _context.Quotes
                .Include(q => q.Request)
                    .ThenInclude(r => r.Client)
                        .ThenInclude(c => c.User)
                .Where(q => q.VendorId == vendor.Id)
                .OrderByDescending(q => q.SubmittedAt)
                .ToListAsync();

            return View(quotes);
        }

        // === MANAGE PROJECTS ===
        public async Task<IActionResult> MyProjects()
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

        // === UPDATE PROJECT (GET) ===
        [HttpGet]
        public async Task<IActionResult> UpdateProject(int id)
        {
            var project = await _context.Projects
                .Include(p => p.Quote)
                    .ThenInclude(q => q.Request)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (project == null)
                return NotFound();

            return View(project);
        }

        // === UPDATE PROJECT (POST) ===
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProject(Project model)
        {
            var project = await _context.Projects.FindAsync(model.Id);
            if (project == null)
                return NotFound();

            project.Status = model.Status;
            project.InstallationNotes = model.InstallationNotes;
            project.MaterialsUsed = model.MaterialsUsed;
            project.ProgressStage = model.ProgressStage;

            if (model.Status == "Completed")
                project.CompletionDate = System.DateTime.UtcNow;

            await _context.SaveChangesAsync();

            TempData["Success"] = "✅ Project updated successfully!";
            return RedirectToAction("MyProjects");
        }

        // === PRODUCTS ===
        public async Task<IActionResult> Products()
        {
            var userEmail = User.Identity.Name;
            var vendor = await _context.Vendors
                .Include(v => v.User)
                .FirstOrDefaultAsync(v => v.User.Email == userEmail);

            if (vendor == null)
                return Unauthorized();

            var products = await _context.Products
                .Where(p => p.VendorId == vendor.Id)
                .ToListAsync();

            return View(products);
        }

        // === REVIEWS ===
        public async Task<IActionResult> Reviews()
        {
            var userEmail = User.Identity.Name;
            var vendor = await _context.Vendors
                .Include(v => v.User)
                .FirstOrDefaultAsync(v => v.User.Email == userEmail);

            if (vendor == null)
                return Unauthorized();

            var reviews = await _context.Reviews
                .Include(r => r.Client)
                    .ThenInclude(c => c.User)
                .Where(r => r.VendorId == vendor.Id)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            return View(reviews);
        }
    }
}
