using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SolarConnect.Data;
using SolarConnect.Models.ViewModels;
using SolarConnect.Models;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using SolarConnect.Models.ViewModels;

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

        public async Task<IActionResult> Dashboard()
        {
            var userId = int.Parse(User.FindFirstValue("UserId"));
            var vendor = await _context.Vendors
                .Include(v => v.User)
                .FirstOrDefaultAsync(v => v.UserId == userId);

            if (vendor == null)
            {
                return NotFound();
            }

            return View(vendor);
        }

        // ADD THIS NEW METHOD
        public async Task<IActionResult> BrowseRequests()
        {
            var userId = int.Parse(User.FindFirstValue("UserId"));
            var vendor = await _context.Vendors.FirstOrDefaultAsync(v => v.UserId == userId);

            if (vendor == null)
            {
                return NotFound();
            }

            // Check if vendor is approved
            if (!vendor.IsApproved)
            {
                TempData["Error"] = "Your account must be approved by admin before you can browse requests.";
                return RedirectToAction("Dashboard");
            }

            // Get all open requests with client info
            var requests = await _context.Requests
                .Include(r => r.Client)
                .ThenInclude(c => c.User)
                .Where(r => r.Status == "Open")
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            return View(requests);
        }
        // GET: Submit Quote
        [HttpGet]
        public async Task<IActionResult> SubmitQuote(int id)
        {
            var userId = int.Parse(User.FindFirstValue("UserId"));
            var vendor = await _context.Vendors.FirstOrDefaultAsync(v => v.UserId == userId);

            if (vendor == null || !vendor.IsApproved)
            {
                TempData["Error"] = "Your account must be approved to submit quotes.";
                return RedirectToAction("Dashboard");
            }

            // Get the request details
            var request = await _context.Requests
                .Include(r => r.Client)
                .ThenInclude(c => c.User)
                .FirstOrDefaultAsync(r => r.Id == id && r.Status == "Open");

            if (request == null)
            {
                TempData["Error"] = "This request is no longer available.";
                return RedirectToAction("BrowseRequests");
            }

            // Check if vendor already submitted a quote for this request
            var existingQuote = await _context.Quotes
                .FirstOrDefaultAsync(q => q.RequestId == id && q.VendorId == vendor.Id);

            if (existingQuote != null)
            {
                TempData["Error"] = "You have already submitted a quote for this request.";
                return RedirectToAction("BrowseRequests");
            }

            // Pass request data to view
            ViewBag.Request = request;
            ViewBag.VendorId = vendor.Id;

            var model = new SubmitQuoteViewModel
            {
                RequestId = id,
                SystemSize = request.RecommendedSystemSize ?? 0
            };

            return View(model);
        }

        // POST: Submit Quote
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitQuote(SubmitQuoteViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var request = await _context.Requests
                    .Include(r => r.Client)
                    .ThenInclude(c => c.User)
                    .FirstOrDefaultAsync(r => r.Id == model.RequestId);
                ViewBag.Request = request;
                return View(model);
            }

            var userId = int.Parse(User.FindFirstValue("UserId"));
            var vendor = await _context.Vendors.FirstOrDefaultAsync(v => v.UserId == userId);

            if (vendor == null || !vendor.IsApproved)
            {
                TempData["Error"] = "Your account must be approved to submit quotes.";
                return RedirectToAction("Dashboard");
            }

            // Create the quote
            var quote = new Quote
            {
                RequestId = model.RequestId,
                VendorId = vendor.Id,
                SystemSize = model.SystemSize,
                TotalPrice = model.TotalPrice,
                InstallationCost = model.InstallationCost,
                PanelBrand = model.PanelBrand,
                InverterBrand = model.InverterBrand,
                BatteryBrand = model.BatteryBrand ?? "",
                SystemDescription = model.SystemDescription,
                EstimatedInstallationDays = model.EstimatedInstallationDays,
                WarrantyYears = model.WarrantyYears,
                Status = "Pending",
                SubmittedAt = DateTime.UtcNow
            };

            _context.Quotes.Add(quote);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Your quote has been submitted successfully! The client will review it soon.";
            return RedirectToAction("BrowseRequests");
        }
    }
}