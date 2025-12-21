using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SolarConnect.Data;
using SolarConnect.Models;
using SolarConnect.Models.ViewModels;
using SolarConnect.Services;
using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SolarConnect.Controllers
{
    [Authorize(Roles = "Vendor")]
    public class VendorController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _emailService;
        private readonly IPdfService _pdfService;

        public VendorController(ApplicationDbContext context, IEmailService emailService, IPdfService pdfService)
        {
            _context = context;
            _emailService = emailService;
            _pdfService = pdfService;
        }

        // ============================================
        // DASHBOARD
        // ============================================
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

            // GET REAL STATISTICS
            // Count available requests (only Open requests)
            var availableRequests = await _context.Requests
                .CountAsync(r => r.Status == "Open");

            // Count vendor's submitted quotes
            var myQuotes = await _context.Quotes
                .CountAsync(q => q.VendorId == vendor.Id);

            // Count accepted projects (vendor's accepted quotes)
            var acceptedProjects = await _context.Quotes
                .CountAsync(q => q.VendorId == vendor.Id && q.Status == "Accepted");

            // ⭐ ADD THIS: Count vendor's products
            var totalProducts = await _context.Products
                .CountAsync(p => p.VendorId == vendor.Id);

            // Pass stats to view
            ViewBag.AvailableRequests = availableRequests;
            ViewBag.MyQuotes = myQuotes;
            ViewBag.AcceptedProjects = acceptedProjects;
            ViewBag.TotalProducts = totalProducts; // ⭐ ADD THIS LINE

            return View(vendor);
        }

        // ============================================
        // BROWSE REQUESTS
        // ============================================
        public async Task<IActionResult> BrowseRequests()
        {
            var userId = int.Parse(User.FindFirstValue("UserId"));
            var vendor = await _context.Vendors.FirstOrDefaultAsync(v => v.UserId == userId);

            if (vendor == null)
            {
                return NotFound();
            }

            if (!vendor.IsApproved)
            {
                TempData["Error"] = "Your account must be approved by admin before you can browse requests.";
                return RedirectToAction("Dashboard");
            }

            var requests = await _context.Requests
                .Include(r => r.Client)
                .ThenInclude(c => c.User)
                .Where(r => r.Status == "Open")
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            return View(requests);
        }

        // ============================================
        // SUBMIT QUOTE
        // ============================================
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

            var request = await _context.Requests
                .Include(r => r.Client)
                .ThenInclude(c => c.User)
                .FirstOrDefaultAsync(r => r.Id == id && r.Status == "Open");

            if (request == null)
            {
                TempData["Error"] = "This request is no longer available.";
                return RedirectToAction("BrowseRequests");
            }

            var existingQuote = await _context.Quotes
                .FirstOrDefaultAsync(q => q.RequestId == id && q.VendorId == vendor.Id);

            if (existingQuote != null)
            {
                TempData["Error"] = "You have already submitted a quote for this request.";
                return RedirectToAction("BrowseRequests");
            }

            ViewBag.Request = request;
            ViewBag.VendorId = vendor.Id;

            var model = new SubmitQuoteViewModel
            {
                RequestId = id,
                SystemSize = request.RecommendedSystemSize ?? 0
            };

            return View(model);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitQuote(SubmitQuoteViewModel model)
        {
            // Use Temp folder instead of C:\ (no permission issues)
            string logPath = Path.Combine(Path.GetTempPath(), "solarconnect_quote_log.txt");

            // ===== TEST: Write to file to confirm method is being called =====
            try
            {
                System.IO.File.AppendAllText(logPath,
                    $"[{DateTime.Now}] SubmitQuote POST called. RequestId: {model.RequestId}, TotalPrice: {model.TotalPrice}\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine("LOG ERROR: " + ex.Message);
            }
            // ===== END TEST =====

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
            var vendor = await _context.Vendors
                .Include(v => v.User)
                .FirstOrDefaultAsync(v => v.UserId == userId);

            if (vendor == null || !vendor.IsApproved)
            {
                TempData["Error"] = "Your account must be approved to submit quotes.";
                return RedirectToAction("Dashboard");
            }

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
                SubmittedAt = DateTime.UtcNow,
                ProjectStatus = "Pending Start",
                ProgressPercentage = 0,
                VendorNotes = ""
            };

            _context.Quotes.Add(quote);
            await _context.SaveChangesAsync();

            // Write to log file that quote was saved
            try
            {
                System.IO.File.AppendAllText(logPath,
                    $"[{DateTime.Now}] Quote saved! Quote ID: {quote.Id}\n");
            }
            catch { }

            // ========== EMAIL NOTIFICATION WITH PDF ==========
            try
            {
                System.IO.File.AppendAllText(logPath,
                    $"[{DateTime.Now}] Starting email process...\n");

                // Get full quote details with all related data
                var fullQuote = await _context.Quotes
                    .Include(q => q.Vendor)
                        .ThenInclude(v => v.User)
                    .Include(q => q.Request)
                        .ThenInclude(r => r.Client)
                            .ThenInclude(c => c.User)
                    .FirstOrDefaultAsync(q => q.Id == quote.Id);

                if (fullQuote != null && fullQuote.Request?.Client?.User != null)
                {
                    var clientEmail = fullQuote.Request.Client.User.Email;
                    var clientName = fullQuote.Request.Client.User.FirstName + " " + fullQuote.Request.Client.User.LastName;

                    System.IO.File.AppendAllText(logPath,
                        $"[{DateTime.Now}] Client Email: {clientEmail}, Name: {clientName}\n");
                    System.IO.File.AppendAllText(logPath,
                        $"[{DateTime.Now}] Vendor Company: {vendor.CompanyName}\n");

                    // Generate PDF
                    System.IO.File.AppendAllText(logPath,
                        $"[{DateTime.Now}] Generating PDF...\n");

                    byte[] pdfBytes = _pdfService.GenerateQuotePdf(
                        fullQuote,
                        fullQuote.Vendor,
                        fullQuote.Request,
                        fullQuote.Request.Client
                    );

                    System.IO.File.AppendAllText(logPath,
                        $"[{DateTime.Now}] PDF Generated: {pdfBytes.Length} bytes\n");

                    // Send Email with PDF attachment
                    System.IO.File.AppendAllText(logPath,
                        $"[{DateTime.Now}] Sending email to {clientEmail}...\n");

                    await _emailService.SendQuoteEmailAsync(
                        recipientEmail: clientEmail,
                        recipientName: clientName,
                        quotePdf: pdfBytes,
                        vendorCompany: vendor.CompanyName,
                        quotePrice: quote.TotalPrice
                    );

                    System.IO.File.AppendAllText(logPath,
                        $"[{DateTime.Now}] ✅ Email sent successfully!\n");
                    System.IO.File.AppendAllText(logPath,
                        $"[{DateTime.Now}] Log file location: {logPath}\n");

                    TempData["Success"] = "✅ Quote submitted successfully and email sent to client!";
                }
                else
                {
                    System.IO.File.AppendAllText(logPath,
                        $"[{DateTime.Now}] ERROR: fullQuote or client data is null\n");
                    TempData["Warning"] = "⚠️ Quote submitted, but email notification could not be sent (client data missing).";
                }
            }
            catch (Exception emailEx)
            {
                // Quote was saved successfully, but email failed
                try
                {
                    System.IO.File.AppendAllText(logPath,
                        $"[{DateTime.Now}] ❌ EMAIL ERROR: {emailEx.Message}\n");
                    System.IO.File.AppendAllText(logPath,
                        $"Stack Trace: {emailEx.StackTrace}\n");
                    if (emailEx.InnerException != null)
                    {
                        System.IO.File.AppendAllText(logPath,
                            $"Inner Exception: {emailEx.InnerException.Message}\n");
                    }
                }
                catch { }

                TempData["Warning"] = "⚠️ Quote submitted but email notification failed: " + emailEx.Message;
            }
            // ========== END EMAIL NOTIFICATION ==========

            return RedirectToAction("BrowseRequests");
        }

        // ============================================
        // MY QUOTES
        // ============================================
        public async Task<IActionResult> MyQuotes()
        {
            var userId = int.Parse(User.FindFirstValue("UserId"));
            var vendor = await _context.Vendors.FirstOrDefaultAsync(v => v.UserId == userId);

            if (vendor == null)
            {
                return NotFound();
            }

            var quotes = await _context.Quotes
                .Include(q => q.Request)
                .ThenInclude(r => r.Client)
                .ThenInclude(c => c.User)
                .Where(q => q.VendorId == vendor.Id)
                .OrderByDescending(q => q.SubmittedAt)
                .ToListAsync();

            return View(quotes);
        }

        // ============================================
        // MY PROJECTS
        // ============================================
        public async Task<IActionResult> MyProjects()
        {
            var userId = int.Parse(User.FindFirstValue("UserId"));
            var vendor = await _context.Vendors.FirstOrDefaultAsync(v => v.UserId == userId);

            if (vendor == null)
            {
                return NotFound();
            }

            var projects = await _context.Quotes
                .Include(q => q.Request)
                .ThenInclude(r => r.Client)
                .ThenInclude(c => c.User)
                .Where(q => q.VendorId == vendor.Id && (q.Status == "Accepted" || q.Status == "Completed"))
                .OrderByDescending(q => q.RespondedAt)
                .ToListAsync();

            return View(projects);
        }

        // ============================================
        // UPDATE PROGRESS
        // ============================================
        [HttpGet]
        public async Task<IActionResult> UpdateProgress(int id)
        {
            var userId = int.Parse(User.FindFirstValue("UserId"));
            var vendor = await _context.Vendors.FirstOrDefaultAsync(v => v.UserId == userId);

            if (vendor == null)
            {
                return NotFound();
            }

            var project = await _context.Quotes
                .Include(q => q.Request)
                .ThenInclude(r => r.Client)
                .ThenInclude(c => c.User)
                .FirstOrDefaultAsync(q => q.Id == id && q.VendorId == vendor.Id && (q.Status == "Accepted" || q.Status == "Completed"));

            if (project == null)
            {
                TempData["Error"] = "Project not found.";
                return RedirectToAction("MyProjects");
            }

            return View(project);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProgress(int id, string projectStatus, int progressPercentage, string vendorNotes)
        {
            var userId = int.Parse(User.FindFirstValue("UserId"));
            var vendor = await _context.Vendors.FirstOrDefaultAsync(v => v.UserId == userId);

            if (vendor == null)
            {
                return NotFound();
            }

            var project = await _context.Quotes.FindAsync(id);

            if (project == null || project.VendorId != vendor.Id)
            {
                TempData["Error"] = "Project not found.";
                return RedirectToAction("MyProjects");
            }

            project.ProjectStatus = projectStatus;
            project.ProgressPercentage = progressPercentage;
            project.VendorNotes = vendorNotes;

            if (projectStatus != "Pending Start" && project.ProjectStartDate == null)
            {
                project.ProjectStartDate = DateTime.UtcNow;
            }

            if (projectStatus == "Completed" || progressPercentage >= 100)
            {
                project.ProjectStatus = "Completed";
                project.ProgressPercentage = 100;
                project.Status = "Completed";

                if (project.ProjectCompletionDate == null)
                {
                    project.ProjectCompletionDate = DateTime.UtcNow;
                }
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = "Project progress updated successfully!";
            return RedirectToAction("MyProjects");
        }

        // ============================================
        // PRODUCTS CATALOG
        // ============================================
        [HttpGet]
        public async Task<IActionResult> Products()
        {
            var userId = int.Parse(User.FindFirstValue("UserId"));
            var vendor = await _context.Vendors.FirstOrDefaultAsync(v => v.UserId == userId);

            if (vendor == null || !vendor.IsApproved)
            {
                TempData["Error"] = "You must be an approved vendor to manage products.";
                return RedirectToAction("Dashboard");
            }

            var products = await _context.Products
                .Where(p => p.VendorId == vendor.Id)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            return View(products);
        }

        // GET: Add Product
        [HttpGet]
        public async Task<IActionResult> AddProduct()
        {
            var userId = int.Parse(User.FindFirstValue("UserId"));
            var vendor = await _context.Vendors.FirstOrDefaultAsync(v => v.UserId == userId);

            if (vendor == null || !vendor.IsApproved)
            {
                TempData["Error"] = "You must be an approved vendor to add products.";
                return RedirectToAction("Dashboard");
            }

            return View(new Product());
        }

        // POST: Add Product - SIMPLE VERSION
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddProduct(
            string ProductName,
            string Category,
            string Brand,
            string Model,
            decimal Price,
            string PowerOutput,
            string Description,
            string Specifications,
            int WarrantyYears,
            int StockQuantity,
            IFormFile ProductImage)
        {
            var userId = int.Parse(User.FindFirstValue("UserId"));
            var vendor = await _context.Vendors.FirstOrDefaultAsync(v => v.UserId == userId);

            if (vendor == null || !vendor.IsApproved)
            {
                TempData["Error"] = "You must be an approved vendor.";
                return RedirectToAction("Dashboard");
            }

            // Validation
            if (string.IsNullOrEmpty(ProductName))
            {
                TempData["Error"] = "Product name is required.";
                return View(new Product());
            }

            if (string.IsNullOrEmpty(Category))
            {
                TempData["Error"] = "Category is required.";
                return View(new Product());
            }

            if (string.IsNullOrEmpty(Brand))
            {
                TempData["Error"] = "Brand is required.";
                return View(new Product());
            }

            if (Price <= 0)
            {
                TempData["Error"] = "Price must be greater than 0.";
                return View(new Product());
            }

            if (string.IsNullOrEmpty(Description))
            {
                TempData["Error"] = "Description is required.";
                return View(new Product());
            }

            // Handle Image Upload
            string imageUrl = null;
            if (ProductImage != null && ProductImage.Length > 0)
            {
                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(ProductImage.FileName);
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "products");

                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                var filePath = Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await ProductImage.CopyToAsync(stream);
                }

                imageUrl = "/images/products/" + fileName;
            }

            var product = new Product
            {
                VendorId = vendor.Id,
                ProductName = ProductName,
                Category = Category,
                Brand = Brand,
                Model = Model ?? "",
                Price = Price,
                PowerOutput = PowerOutput ?? "",
                Description = Description,
                Specifications = Specifications ?? "",
                WarrantyYears = WarrantyYears,
                StockQuantity = StockQuantity,
                ImageUrl = imageUrl,
                IsAvailable = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Product added successfully!";
            return RedirectToAction("Products");
        }

        [HttpGet]
        public async Task<IActionResult> EditProduct(int id)
        {
            var userId = int.Parse(User.FindFirstValue("UserId"));
            var vendor = await _context.Vendors.FirstOrDefaultAsync(v => v.UserId == userId);

            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.Id == id && p.VendorId == vendor.Id);

            if (product == null)
            {
                TempData["Error"] = "Product not found.";
                return RedirectToAction("Products");
            }

            var model = new CreateProductViewModel
            {
                ProductName = product.ProductName,
                Category = product.Category,
                Brand = product.Brand,
                Model = product.Model,
                Price = product.Price,
                PowerOutput = product.PowerOutput,
                Description = product.Description,
                Specifications = product.Specifications,
                WarrantyYears = product.WarrantyYears,
                StockQuantity = product.StockQuantity,
                ImageUrl = product.ImageUrl
            };

            ViewBag.ProductId = id;
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProduct(int id, CreateProductViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.ProductId = id;
                return View(model);
            }

            var userId = int.Parse(User.FindFirstValue("UserId"));
            var vendor = await _context.Vendors.FirstOrDefaultAsync(v => v.UserId == userId);

            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.Id == id && p.VendorId == vendor.Id);

            if (product == null)
            {
                TempData["Error"] = "Product not found.";
                return RedirectToAction("Products");
            }

            // Handle new image upload
            if (model.ProductImage != null && model.ProductImage.Length > 0)
            {
                // Delete old image
                if (!string.IsNullOrEmpty(product.ImageUrl))
                {
                    var oldImagePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", product.ImageUrl.TrimStart('/'));
                    if (System.IO.File.Exists(oldImagePath))
                    {
                        System.IO.File.Delete(oldImagePath);
                    }
                }

                // Save new image
                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(model.ProductImage.FileName);
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "products");

                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                var filePath = Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await model.ProductImage.CopyToAsync(stream);
                }

                product.ImageUrl = "/images/products/" + fileName;
            }

            product.ProductName = model.ProductName;
            product.Category = model.Category;
            product.Brand = model.Brand;
            product.Model = model.Model;
            product.Price = model.Price;
            product.PowerOutput = model.PowerOutput;
            product.Description = model.Description;
            product.Specifications = model.Specifications;
            product.WarrantyYears = model.WarrantyYears;
            product.StockQuantity = model.StockQuantity;
            product.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            TempData["Success"] = "Product updated successfully!";
            return RedirectToAction("Products");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            var userId = int.Parse(User.FindFirstValue("UserId"));
            var vendor = await _context.Vendors.FirstOrDefaultAsync(v => v.UserId == userId);

            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.Id == id && p.VendorId == vendor.Id);

            if (product == null)
            {
                TempData["Error"] = "Product not found.";
                return RedirectToAction("Products");
            }

            // Delete image if exists
            if (!string.IsNullOrEmpty(product.ImageUrl))
            {
                var imagePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", product.ImageUrl.TrimStart('/'));
                if (System.IO.File.Exists(imagePath))
                {
                    System.IO.File.Delete(imagePath);
                }
            }

            _context.Products.Remove(product);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Product deleted successfully!";
            return RedirectToAction("Products");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleAvailability(int id)
        {
            var userId = int.Parse(User.FindFirstValue("UserId"));
            var vendor = await _context.Vendors.FirstOrDefaultAsync(v => v.UserId == userId);

            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.Id == id && p.VendorId == vendor.Id);

            if (product == null)
            {
                TempData["Error"] = "Product not found.";
                return RedirectToAction("Products");
            }

            product.IsAvailable = !product.IsAvailable;
            product.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Product {(product.IsAvailable ? "enabled" : "disabled")} successfully!";
            return RedirectToAction("Products");
        }
    }
}