using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SolarConnect.Data;
using SolarConnect.Models.ViewModels;
using System.Linq;
using System.Threading.Tasks;

namespace SolarConnect.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Dashboard()
        {
            var model = new AdminDashboardViewModel
            {
                TotalClients = await _context.Clients.CountAsync(),
                TotalVendors = await _context.Vendors.CountAsync(),
                PendingApprovals = await _context.Vendors.CountAsync(v => !v.IsApproved),
                ActiveRequests = await _context.Requests.CountAsync(r => r.Status == "Open")
            };

            return View(model);
        }

        public async Task<IActionResult> PendingVendors()
        {
            var pendingVendors = await _context.Vendors
                .Include(v => v.User)
                .Where(v => !v.IsApproved)
                .OrderByDescending(v => v.CreatedAt)
                .ToListAsync();

            return View(pendingVendors);
        }

        [HttpPost]
        public async Task<IActionResult> ApproveVendor(int id)
        {
            var vendor = await _context.Vendors.FindAsync(id);
            if (vendor == null)
                return NotFound();

            vendor.IsApproved = true;
            await _context.SaveChangesAsync();

            TempData["Success"] = "Vendor approved successfully!";
            return RedirectToAction("PendingVendors");
        }

        [HttpPost]
        public async Task<IActionResult> RejectVendor(int id)
        {
            var vendor = await _context.Vendors.Include(v => v.User).FirstOrDefaultAsync(v => v.Id == id);
            if (vendor == null)
                return NotFound();

            _context.Vendors.Remove(vendor);
            _context.Users.Remove(vendor.User);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Vendor rejected and removed.";
            return RedirectToAction("PendingVendors");
        }
        // === MANAGE USERS ===
        public async Task<IActionResult> ManageUsers()
        {
            var users = await _context.Users
                .OrderBy(u => u.Role)
                .ThenBy(u => u.Email)
                .ToListAsync();
            return View(users);
        }

        // === ALL REQUESTS ===
        public async Task<IActionResult> AllRequests()
        {
            var requests = await _context.Requests
                .Include(r => r.Client)
                .ThenInclude(c => c.User)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();
            return View(requests);
        }

        // === PROJECTS (placeholder) ===
        public IActionResult Projects()
        {
            return View();
        }

        // === PRODUCTS (placeholder) ===
        public IActionResult Products()
        {
            return View();
        }

        // === REPORTS & ANALYTICS ===
        public async Task<IActionResult> Reports()
        {
            var summary = new AdminDashboardViewModel
            {
                TotalClients = await _context.Clients.CountAsync(),
                TotalVendors = await _context.Vendors.CountAsync(),
                PendingApprovals = await _context.Vendors.CountAsync(v => !v.IsApproved),
                ActiveRequests = await _context.Requests.CountAsync(r => r.Status == "Open")
            };
            return View(summary);
        }

    }
}
