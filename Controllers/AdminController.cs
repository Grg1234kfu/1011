using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SolarConnect.Data;
using SolarConnect.Models;
using SolarConnect.Models.ViewModels;
using System.Security.Cryptography;
using System.Text;

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

        // ===============================
        // DASHBOARD
        // ===============================
        [HttpGet]
        public async Task<IActionResult> Dashboard()
        {
            ViewBag.TotalClients = await _context.Clients.CountAsync();
            ViewBag.TotalVendors = await _context.Vendors.CountAsync();
            ViewBag.PendingApprovals = await _context.Vendors.CountAsync(v => !v.IsApproved);
            ViewBag.ActiveRequests = await _context.Requests.CountAsync(r => r.Status != "Closed");
            return View();
        }

        // ===============================
        // USERS (Manage Accounts)
        // ===============================
        [HttpGet]
        public async Task<IActionResult> Users()
        {
            var users = await _context.Users
                .OrderByDescending(u => u.CreatedAt)
                .ToListAsync();

            return View(users);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(int id)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (user == null) return NotFound();

            user.IsActive = !user.IsActive;
            await _context.SaveChangesAsync();

            TempData["Success"] = "User status updated.";
            return RedirectToAction(nameof(Users));
        }

        [HttpGet]
        public IActionResult CreateUser()
        {
            return View(new AdminCreateUserViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateUser(AdminCreateUserViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var emailExists = await _context.Users.AnyAsync(u => u.Email == model.Email);
            if (emailExists)
            {
                ModelState.AddModelError(nameof(model.Email), "Email already exists.");
                return View(model);
            }

            var user = new User
            {
                FirstName = model.FirstName.Trim(),
                LastName = model.LastName.Trim(),
                Email = model.Email.Trim(),
                PhoneNumber = model.PhoneNumber?.Trim() ?? "",
                Role = model.Role,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                Password = HashPassword(model.Password)
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            if (model.Role == "Client")
            {
                _context.Clients.Add(new Client
                {
                    UserId = user.Id,
                    Address = model.Address ?? "",
                    PropertyType = model.PropertyType ?? "",
                    AdditionalNotes = model.AdditionalNotes ?? "",
                    CreatedAt = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();
            }

            if (model.Role == "Vendor")
            {
                _context.Vendors.Add(new Vendor
                {
                    UserId = user.Id,
                    CompanyName = model.CompanyName ?? "New Vendor",
                    BusinessLicense = model.BusinessLicense ?? "N/A",
                    YearsOfExperience = model.YearsOfExperience ?? 0,
                    ServiceArea = model.ServiceArea ?? "N/A",
                    Specialization = model.Specialization ?? "N/A",
                    Website = model.Website ?? "N/A",
                    CompanyDescription = model.CompanyDescription ?? "N/A",
                    IsApproved = model.IsApproved ?? false,
                    Rating = 0,
                    CreatedAt = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();
            }

            TempData["Success"] = "User created successfully!";
            return RedirectToAction(nameof(Users));
        }

        [HttpGet]
        public async Task<IActionResult> EditUser(int id)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (user == null) return NotFound();

            var vm = new AdminEditUserViewModel
            {
                Id = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                Role = user.Role,
                IsActive = user.IsActive
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUser(AdminEditUserViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == model.Id);
            if (user == null) return NotFound();

            var emailTaken = await _context.Users.AnyAsync(u => u.Email == model.Email && u.Id != model.Id);
            if (emailTaken)
            {
                ModelState.AddModelError(nameof(model.Email), "Email already exists.");
                return View(model);
            }

            user.FirstName = model.FirstName.Trim();
            user.LastName = model.LastName.Trim();
            user.Email = model.Email.Trim();
            user.PhoneNumber = model.PhoneNumber?.Trim() ?? "";
            user.Role = model.Role;
            user.IsActive = model.IsActive;

            await _context.SaveChangesAsync();

            TempData["Success"] = "User updated.";
            return RedirectToAction(nameof(Users));
        }

        [HttpGet]
        public async Task<IActionResult> ChangePassword(int id)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (user == null) return NotFound();

            return View(new AdminChangePasswordViewModel
            {
                UserId = user.Id,
                Email = user.Email
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(AdminChangePasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == model.UserId);
            if (user == null) return NotFound();

            user.Password = HashPassword(model.NewPassword);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Password changed successfully.";
            return RedirectToAction(nameof(Users));
        }

        [HttpGet]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (user == null) return NotFound();

            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUserConfirmed(int id)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (user == null) return NotFound();

            if (user.Role == "Admin")
            {
                TempData["Error"] = "You cannot delete an Admin.";
                return RedirectToAction(nameof(Users));
            }

            var client = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == id);
            if (client != null) _context.Clients.Remove(client);

            var vendor = await _context.Vendors.FirstOrDefaultAsync(v => v.UserId == id);
            if (vendor != null) _context.Vendors.Remove(vendor);

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            TempData["Success"] = "User deleted.";
            return RedirectToAction(nameof(Users));
        }

        // ===============================
        // VENDOR APPROVAL
        // ===============================
        [HttpGet]
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
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveVendor(int id)
        {
            var vendor = await _context.Vendors.FirstOrDefaultAsync(v => v.Id == id);
            if (vendor == null) return NotFound();

            vendor.IsApproved = true;
            await _context.SaveChangesAsync();

            TempData["Success"] = "Vendor approved successfully!";
            return RedirectToAction(nameof(PendingVendors));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectVendor(int id)
        {
            var vendor = await _context.Vendors.Include(v => v.User).FirstOrDefaultAsync(v => v.Id == id);
            if (vendor == null) return NotFound();

            _context.Vendors.Remove(vendor);
            _context.Users.Remove(vendor.User);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Vendor rejected and removed.";
            return RedirectToAction(nameof(PendingVendors));
        }

        // ===============================
        // DASHBOARD LINKS PAGES
        // ===============================

        [HttpGet]
        public async Task<IActionResult> Products()
        {
            var products = await _context.Products
                .Include(p => p.Vendor)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            return View(products);
        }

        [HttpGet]
        public async Task<IActionResult> Requests()
        {
            var requests = await _context.Requests
                .Include(r => r.Client)
                .ThenInclude(c => c.User)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            return View(requests);
        }

        [HttpGet]
        public async Task<IActionResult> Projects()
        {
            var projects = await _context.Quotes
                .Include(q => q.Vendor).ThenInclude(v => v.User)
                .Include(q => q.Request).ThenInclude(r => r.Client).ThenInclude(c => c.User)
                .OrderByDescending(q => q.SubmittedAt)
                .ToListAsync();

            return View(projects);
        }

        [HttpGet]
        public async Task<IActionResult> Reports()
        {
            ViewBag.TotalUsers = await _context.Users.CountAsync();
            ViewBag.TotalClients = await _context.Clients.CountAsync();
            ViewBag.TotalVendors = await _context.Vendors.CountAsync();
            ViewBag.ApprovedVendors = await _context.Vendors.CountAsync(v => v.IsApproved);
            ViewBag.TotalProducts = await _context.Products.CountAsync();
            ViewBag.TotalRequests = await _context.Requests.CountAsync();
            ViewBag.TotalQuotes = await _context.Quotes.CountAsync();
            return View();
        }

        // ===============================
        // ACTIONS (Requests + Quotes) + filters
        // /Admin/Actions?searchEmail=..&clientId=..&vendorId=..&onlyNoQuotes=true
        // ===============================
        [HttpGet]
        public async Task<IActionResult> Actions(string? searchEmail, int? clientId, int? vendorId, bool onlyNoQuotes = false)
        {
            // dropdown lists
            var clientsList = await _context.Clients
                .Include(c => c.User)
                .Select(c => new { c.Id, Email = c.User.Email, Name = (c.User.FirstName + " " + c.User.LastName) })
                .OrderBy(x => x.Email)
                .ToListAsync();

            var vendorsList = await _context.Vendors
                .Include(v => v.User)
                .Select(v => new { v.Id, Email = v.User.Email, Name = (v.User.FirstName + " " + v.User.LastName) })
                .OrderBy(x => x.Email)
                .ToListAsync();

            // base requests query
            var requestsQuery = _context.Requests
                .Include(r => r.Client)
                    .ThenInclude(c => c.User)
                .AsQueryable();

            if (clientId.HasValue)
                requestsQuery = requestsQuery.Where(r => r.ClientId == clientId.Value);

            if (vendorId.HasValue)
            {
                requestsQuery = requestsQuery.Where(r =>
                    _context.Quotes.Any(q => q.RequestId == r.Id && q.VendorId == vendorId.Value)
                );
            }

            if (!string.IsNullOrWhiteSpace(searchEmail))
            {
                var s = searchEmail.Trim().ToLower();

                requestsQuery = requestsQuery.Where(r =>
                    r.Client.User.Email.ToLower().Contains(s) ||
                    _context.Quotes
                        .Include(q => q.Vendor).ThenInclude(v => v.User)
                        .Any(q => q.RequestId == r.Id && q.Vendor.User.Email.ToLower().Contains(s))
                );
            }

            var requests = await requestsQuery
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            var requestIds = requests.Select(r => r.Id).ToList();

            var quotes = await _context.Quotes
                .Where(q => requestIds.Contains(q.RequestId))
                .Include(q => q.Vendor).ThenInclude(v => v.User)
                .OrderByDescending(q => q.SubmittedAt)
                .ToListAsync();

            var quotesByRequest = quotes
                .GroupBy(q => q.RequestId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var rows = new List<AdminRequestActionsRowViewModel>();

            foreach (var r in requests)
            {
                quotesByRequest.TryGetValue(r.Id, out var rqQuotes);
                rqQuotes ??= new List<Quote>();

                rows.Add(new AdminRequestActionsRowViewModel
                {
                    RequestId = r.Id,
                    ClientId = r.ClientId,
                    ClientEmail = r.Client?.User?.Email ?? "N/A",
                    ClientName = ((r.Client?.User?.FirstName ?? "") + " " + (r.Client?.User?.LastName ?? "")).Trim(),
                    Status = r.Status,
                    PropertyAddress = r.PropertyAddress,
                    PropertyType = r.PropertyType,
                    CreatedAt = r.CreatedAt,
                    QuotesCount = rqQuotes.Count,
                    Quotes = rqQuotes.Select(q => new AdminQuoteRowViewModel
                    {
                        QuoteId = q.Id,
                        VendorId = q.VendorId,
                        VendorEmail = q.Vendor?.User?.Email ?? "N/A",
                        VendorName = ((q.Vendor?.User?.FirstName ?? "") + " " + (q.Vendor?.User?.LastName ?? "")).Trim(),
                        CompanyName = q.Vendor?.CompanyName ?? "N/A",
                        TotalPrice = q.TotalPrice,
                        Status = q.Status,
                        SubmittedAt = q.SubmittedAt
                    }).ToList()
                });
            }

            if (onlyNoQuotes)
                rows = rows.Where(x => x.QuotesCount == 0).ToList();

            var vm = new AdminActionsPageViewModel
            {
                Filter = new AdminActionsFilterViewModel
                {
                    SearchEmail = searchEmail,
                    ClientId = clientId,
                    VendorId = vendorId,
                    OnlyNoQuotes = onlyNoQuotes
                },
                Rows = rows,
                Clients = clientsList.Select(x => (x.Id, x.Email, x.Name)).ToList(),
                Vendors = vendorsList.Select(x => (x.Id, x.Email, x.Name)).ToList(),
            };

            return View(vm);
        }

        // ===============================
        // PASSWORD HASH
        // ===============================
        private static string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(bytes);
        }
    }
}
