using System;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SolarConnect.Data;
using SolarConnect.Models;
using SolarConnect.Models.ViewModels;
using SolarConnect.Services;


namespace SolarConnect.Controllers
{
    [Authorize(Roles = "Client")]
    public class ClientController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAIQuoteAdvisorService _aiQuoteAdvisorService;
        public ClientController(ApplicationDbContext context, IAIQuoteAdvisorService aiQuoteAdvisorService)
        {
            _context = context;
            _aiQuoteAdvisorService = aiQuoteAdvisorService;
        }

        // GET: Dashboard
        [HttpGet]
        public async Task<IActionResult> Dashboard()
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue("UserId") ?? "0");
                var client = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == userId);

                if (client == null)
                    return RedirectToAction("Login", "Auth");

                // Count total requests
                var totalRequests = await _context.Requests
                    .Where(r => r.ClientId == client.Id)
                    .CountAsync();

                // Count received quotes (pending only)
                var receivedQuotes = await _context.Quotes
                    .Include(q => q.Request)
                    .Where(q => q.Request.ClientId == client.Id && q.Status == "Pending")
                    .CountAsync();

                // Count accepted projects
                var acceptedProjects = await _context.Quotes
                    .Include(q => q.Request)
                    .Where(q => q.Request.ClientId == client.Id && q.Status == "Accepted")
                    .CountAsync();

                var user = await _context.Users.FindAsync(userId);

                ViewBag.UserName = user?.FirstName ?? "Client";
                ViewBag.TotalRequests = totalRequests;
                ViewBag.ReceivedQuotes = receivedQuotes;
                ViewBag.AcceptedProjects = acceptedProjects;

                return View();
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error: {ex.Message}";
                return RedirectToAction("Login", "Auth");
            }
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
            try
            {
                if (!ModelState.IsValid)
                    return View(model);

                var userId = int.Parse(User.FindFirstValue("UserId") ?? "0");
                var client = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == userId);

                if (client == null)
                    return RedirectToAction("Dashboard");

                var recommendedSize = model.MonthlyConsumption / 150m;

                var request = new Request
                {
                    ClientId = client.Id,
                    PropertyAddress = model.PropertyAddress,
                    PropertyType = model.PropertyType,
                    RoofArea = model.RoofArea,
                    MonthlyConsumption = model.MonthlyConsumption,
                    MonthlyBill = model.MonthlyBill,
                    RecommendedSystemSize = (int)recommendedSize,
                    AdditionalNotes = model.AdditionalNotes,
                    Status = "Open",
                    CreatedAt = DateTime.UtcNow
                };

                _context.Requests.Add(request);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Request created successfully!";
                return RedirectToAction("MyRequests");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error creating request: {ex.Message}";
                return View(model);
            }
        }

        // GET: My Requests
        // Update your MyRequests action in ClientController.cs

        [HttpGet]
        public async Task<IActionResult> MyRequests()
        {
            var userId = int.Parse(User.FindFirstValue("UserId"));
            var client = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == userId);

            if (client == null)
            {
                return RedirectToAction("Login", "Auth");
            }

            // Get all requests with their quotes
            var requests = await _context.Requests
                .Where(r => r.ClientId == client.Id)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            // Count quotes for each request
            var quotesCounts = new Dictionary<int, int>();
            foreach (var request in requests)
            {
                var count = await _context.Quotes
                    .Where(q => q.RequestId == request.Id)
                    .CountAsync();
                quotesCounts[request.Id] = count;
            }

            ViewBag.QuotesCounts = quotesCounts;

            return View(requests);
        }

        // GET: Request Details
        [HttpGet]
        public async Task<IActionResult> RequestDetails(int id)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue("UserId") ?? "0");
                var client = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == userId);

                if (client == null)
                    return RedirectToAction("Dashboard");

                var request = await _context.Requests
                    .Include(r => r.Client)
                    .FirstOrDefaultAsync(r => r.Id == id && r.ClientId == client.Id);

                if (request == null)
                {
                    TempData["Error"] = "Request not found.";
                    return RedirectToAction("MyRequests");
                }

                var quotes = await _context.Quotes
                    .Include(q => q.Vendor)
                    .ThenInclude(v => v.User)
                    .Where(q => q.RequestId == id)
                    .OrderByDescending(q => q.SubmittedAt)
                    .ToListAsync();

                ViewBag.Quotes = quotes;
                ViewBag.RequestId = id;

                return View(request);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error: {ex.Message}";
                return RedirectToAction("MyRequests");
            }
        }

        // POST: Accept Quote
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AcceptQuote(int requestId, int quoteId)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue("UserId") ?? "0");
                var client = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == userId);

                if (client == null)
                    return RedirectToAction("Dashboard");

                // Get the quote
                var quote = await _context.Quotes
                    .Include(q => q.Vendor)
                    .FirstOrDefaultAsync(q => q.Id == quoteId && q.RequestId == requestId);

                if (quote == null)
                {
                    TempData["Error"] = "Quote not found.";
                    return RedirectToAction("RequestDetails", new { id = requestId });
                }

                // Accept this quote
                quote.Status = "Accepted";
                quote.RespondedAt = DateTime.UtcNow;

                // Reject all other quotes
                var otherQuotes = await _context.Quotes
                    .Where(q => q.RequestId == requestId && q.Id != quoteId)
                    .ToListAsync();

                foreach (var otherQuote in otherQuotes)
                {
                    otherQuote.Status = "Rejected";
                    otherQuote.RespondedAt = DateTime.UtcNow;
                }

                // Update request status
                var request = await _context.Requests.FindAsync(requestId);
                if (request != null)
                {
                    request.Status = "Accepted";
                    request.ClosedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();

                TempData["Success"] = "Quote accepted successfully!";
                return RedirectToAction("MyProjects");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error accepting quote: {ex.Message}";
                return RedirectToAction("RequestDetails", new { id = requestId });
            }
        }

        // GET: My Projects
        [HttpGet]
        public async Task<IActionResult> MyProjects()
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue("UserId") ?? "0");
                var client = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == userId);

                if (client == null)
                    return RedirectToAction("Dashboard");

                var projects = await _context.Quotes
                    .Include(q => q.Request)
                    .ThenInclude(r => r.Client)
                    .ThenInclude(c => c.User)
                    .Include(q => q.Vendor)
                    .ThenInclude(v => v.User)
                    .Where(q => q.Request.ClientId == client.Id && q.Status == "Accepted")
                    .OrderByDescending(q => q.SubmittedAt)
                    .ToListAsync();

                return View(projects);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error: {ex.Message}";
                return RedirectToAction("Dashboard");
            }
        }
        [HttpGet]
        public async Task<IActionResult> AIQuoteAnalysis(int requestId)
        {
            var userId = int.Parse(User.FindFirstValue("UserId"));
            var client = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == userId);

            if (client == null)
            {
                return RedirectToAction("Login", "Auth");
            }

            // Get the request with all quotes
            var request = await _context.Requests
                .Include(r => r.Client)
                    .ThenInclude(c => c.User)
                .FirstOrDefaultAsync(r => r.Id == requestId && r.ClientId == client.Id);

            if (request == null)
            {
                TempData["Error"] = "Request not found.";
                return RedirectToAction("MyRequests");
            }

            // Get all quotes for this request
            var quotes = await _context.Quotes
                .Include(q => q.Vendor)
                    .ThenInclude(v => v.User)
                .Where(q => q.RequestId == requestId)
                .OrderBy(q => q.TotalPrice)
                .ToListAsync();

            if (quotes.Count == 0)
            {
                TempData["Error"] = "No quotes available for this request yet.";
                return RedirectToAction("MyRequests");
            }

            ViewBag.Request = request;
            ViewBag.QuotesCount = quotes.Count;

            // Get AI analysis
            ViewBag.IsAnalyzing = true;
            var analysis = await _aiQuoteAdvisorService.AnalyzeQuotesAsync(quotes, request);
            ViewBag.AIAnalysis = analysis;
            ViewBag.Quotes = quotes;

            return View();
        }
    }
}