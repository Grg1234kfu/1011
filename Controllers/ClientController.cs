using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SolarConnect.Data;
using SolarConnect.Models;
using System.Linq;
using System.Threading.Tasks;

namespace SolarConnect.Controllers
{
    [Authorize(Roles = "Client")]
    public class ClientController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ClientController(ApplicationDbContext context)
        {
            _context = context;
        }

        // === CLIENT DASHBOARD ===
        public async Task<IActionResult> Dashboard()
        {
            var userEmail = User.Identity.Name;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == userEmail);

            ViewBag.ClientName = user?.FirstName ?? "Client";
            ViewBag.Email = user?.Email;

            return View();
        }

        // === NEW SOLAR REQUEST FORM ===
        [HttpGet]
        public IActionResult NewRequest()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> NewRequest(Request model)
        {
            var userEmail = User.Identity.Name;
            var client = await _context.Clients
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.User.Email == userEmail);

            if (client == null)
                return Unauthorized();

            if (ModelState.IsValid)
            {
                model.ClientId = client.Id;
                model.Status = "Open";
                model.CreatedAt = System.DateTime.UtcNow;

                _context.Requests.Add(model);
                await _context.SaveChangesAsync();

                TempData["Success"] = "✅ Your solar request has been submitted!";
                return RedirectToAction("MyRequests");
            }

            return View(model);
        }

        // === MY REQUESTS ===
        public async Task<IActionResult> MyRequests()
        {
            var userEmail = User.Identity.Name;
            var client = await _context.Clients
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.User.Email == userEmail);

            if (client == null)
                return Unauthorized();

            var requests = await _context.Requests
                .Where(r => r.ClientId == client.Id)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            return View(requests);
        }

        // === RECEIVED QUOTES ===
        public async Task<IActionResult> ReceivedQuotes()
        {
            var userEmail = User.Identity.Name;
            var client = await _context.Clients
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.User.Email == userEmail);

            if (client == null)
                return Unauthorized();

            var quotes = await _context.Quotes
                .Include(q => q.Request)
                    .ThenInclude(r => r.Client)
                .Include(q => q.Vendor)
                    .ThenInclude(v => v.User)
                .Where(q => q.Request.ClientId == client.Id)
                .OrderByDescending(q => q.SubmittedAt)
                .ToListAsync();

            return View(quotes);
        }

        // === ACCEPT QUOTE ===
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AcceptQuote(int quoteId)
        {
            var userEmail = User.Identity.Name;
            var client = await _context.Clients
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.User.Email == userEmail);

            if (client == null)
                return Unauthorized();

            var quote = await _context.Quotes
                .Include(q => q.Request)
                .FirstOrDefaultAsync(q => q.Id == quoteId && q.Request.ClientId == client.Id);

            if (quote == null)
                return NotFound();

            // Mark accepted
            quote.Status = "Accepted";
            quote.Request.Status = "Accepted";
            quote.Request.ClosedAt = System.DateTime.UtcNow;

            // Reject others
            var otherQuotes = await _context.Quotes
                .Where(q => q.RequestId == quote.RequestId && q.Id != quote.Id)
                .ToListAsync();
            foreach (var q in otherQuotes)
                q.Status = "Rejected";

            // ✅ Create project automatically
            var project = new Project
            {
                QuoteId = quote.Id,
                Status = "Pending",
                StartDate = System.DateTime.UtcNow,
                ProgressStage = "Project Created"
            };

            _context.Projects.Add(project);
            await _context.SaveChangesAsync();

            TempData["Success"] = "✅ Quote accepted successfully! Project created and installation process started.";
            return RedirectToAction("ReceivedQuotes");
        }

        // === VIEW MY PROJECTS ===
        public async Task<IActionResult> MyProjects()
        {
            var userEmail = User.Identity.Name;
            var client = await _context.Clients
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.User.Email == userEmail);

            if (client == null)
                return Unauthorized();

            var projects = await _context.Projects
                .Include(p => p.Quote)
                    .ThenInclude(q => q.Vendor)
                        .ThenInclude(v => v.User)
                .Include(p => p.Quote.Request)
                .Where(p => p.Quote.Request.ClientId == client.Id)
                .OrderByDescending(p => p.StartDate)
                .ToListAsync();

            return View(projects);
        }
    }
}
