using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SolarConnect.Data;
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

        public IActionResult Dashboard()
        {
            return View();
        }

        // GET: Pending Vendors
        public async Task<IActionResult> PendingVendors()
        {
            var pendingVendors = await _context.Vendors
                .Include(v => v.User)
                .Where(v => !v.IsApproved)
                .OrderByDescending(v => v.CreatedAt)
                .ToListAsync();

            return View(pendingVendors);
        }

        // POST: Approve Vendor
        [HttpPost]
        public async Task<IActionResult> ApproveVendor(int id)
        {
            var vendor = await _context.Vendors.FindAsync(id);
            if (vendor == null)
            {
                return NotFound();
            }

            vendor.IsApproved = true;
            await _context.SaveChangesAsync();

            TempData["Success"] = "Vendor approved successfully!";
            return RedirectToAction("PendingVendors");
        }

        // POST: Reject Vendor
        [HttpPost]
        public async Task<IActionResult> RejectVendor(int id)
        {
            var vendor = await _context.Vendors.Include(v => v.User).FirstOrDefaultAsync(v => v.Id == id);
            if (vendor == null)
            {
                return NotFound();
            }

            // Delete vendor and user
            _context.Vendors.Remove(vendor);
            _context.Users.Remove(vendor.User);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Vendor rejected and removed.";
            return RedirectToAction("PendingVendors");
        }
    }
}