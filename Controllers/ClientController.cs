using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SolarConnect.Data;
using SolarConnect.Models;
using SolarConnect.Models.ViewModels;
using System;
using System.Linq;
using System.Security.Claims;
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

        public async Task<IActionResult> Dashboard()
        {
            var userId = int.Parse(User.FindFirstValue("UserId"));
            var client = await _context.Clients
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (client == null)
            {
                return NotFound();
            }

            // Get client's requests count
            var requestsCount = await _context.Requests.CountAsync(r => r.ClientId == client.Id);
            ViewBag.RequestsCount = requestsCount;

            return View(client);
        }

        // GET: Create Request
        [HttpGet]
        public IActionResult CreateRequest()
        {
            return View();
        }

        // POST: Create Request
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateRequest(CreateRequestViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var userId = int.Parse(User.FindFirstValue("UserId"));
            var client = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == userId);

            if (client == null)
            {
                return NotFound();
            }

            // Calculate recommended system size (simple formula: consumption / 150)
            var recommendedSize = model.MonthlyConsumption / 150;

            var request = new Request
            {
                ClientId = client.Id,
                PropertyAddress = model.PropertyAddress,
                PropertyType = model.PropertyType,
                RoofArea = model.RoofArea,
                MonthlyConsumption = model.MonthlyConsumption,
                MonthlyBill = model.MonthlyBill,
                RecommendedSystemSize = recommendedSize,
                AdditionalNotes = model.AdditionalNotes,
                Status = "Open",
                CreatedAt = DateTime.UtcNow
            };

            _context.Requests.Add(request);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Your solar request has been created! Vendors will start sending quotes soon.";
            return RedirectToAction("MyRequests");
        }

        // GET: My Requests
        public async Task<IActionResult> MyRequests()
        {
            var userId = int.Parse(User.FindFirstValue("UserId"));
            var client = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == userId);

            if (client == null)
            {
                return NotFound();
            }

            var requests = await _context.Requests
                .Where(r => r.ClientId == client.Id)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            return View(requests);
        }

        // GET: Request Details with Quotes
        public async Task<IActionResult> RequestDetails(int id)
        {
            var userId = int.Parse(User.FindFirstValue("UserId"));
            var client = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == userId);

            if (client == null)
            {
                return NotFound();
            }

            var request = await _context.Requests
                .Include(r => r.Client)
                .ThenInclude(c => c.User)
                .FirstOrDefaultAsync(r => r.Id == id && r.ClientId == client.Id);

            if (request == null)
            {
                return NotFound();
            }

            // Get quotes for this request
            var quotes = await _context.Quotes
                .Include(q => q.Vendor)
                .ThenInclude(v => v.User)
                .Where(q => q.RequestId == id)
                .OrderByDescending(q => q.SubmittedAt)
                .ToListAsync();

            ViewBag.Quotes = quotes;

            return View(request);
        }
    }
}