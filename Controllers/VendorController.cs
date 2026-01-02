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

            // ⭐ VENDOR RATING CALCULATION
            var ratedCompletedProjects = await _context.Quotes
                .Where(q =>
                    q.VendorId == vendor.Id &&
                    q.ProgressPercentage >= 100 &&
                    q.ClientRating != null
                )
                .ToListAsync();

            double averageRating = 0;
            int totalRatings = ratedCompletedProjects.Count;

            if (totalRatings > 0)
            {
                averageRating = Math.Round(
                    ratedCompletedProjects.Average(q => q.ClientRating.Value),
                    1
                );
            }

            // Pass to view
            ViewBag.AverageRating = averageRating;
            ViewBag.TotalRatings = totalRatings;


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
            var vendor = await _context.Vendors
                .Include(v => v.User)
                .FirstOrDefaultAsync(v => v.UserId == userId);

            if (vendor == null)
            {
                TempData["Error"] = "Vendor account not found.";
                return RedirectToAction("Dashboard");
            }

            // ✅ CRITICAL FIX: ONLY get quotes with Status = "Accepted"
            // Do NOT check for "Completed" - that's tracked via ProgressPercentage
            var projects = await _context.Quotes
                .Include(q => q.Request)
                    .ThenInclude(r => r.Client)
                        .ThenInclude(c => c.User)
                .Where(q => q.VendorId == vendor.Id && q.Status == "Accepted")  // ✅ ONLY Accepted
                .OrderByDescending(q => q.RespondedAt)
                .ToListAsync();

            // Update overdue status for all projects
            foreach (var project in projects)
            {
                UpdateOverdueStatus(project);
            }

            await _context.SaveChangesAsync();

            return View(projects);
        }


        // ============================================
        // UPDATE PROGRESS
        // ============================================
        // ============================================
        // CORRECTED UPDATE PROGRESS METHODS
        // Replace your existing UpdateProgress methods with these
        // ============================================

        // ============================================================================
        // COMPLETE REPLACEMENT FOR YOUR UpdateProgress METHODS
        // Replace BOTH [HttpGet] and [HttpPost] UpdateProgress in VendorController.cs
        // ============================================================================

        // ============================================================================
        // UpdateProgress - GET (Show Form)
        // ============================================================================

        [HttpGet]
        public async Task<IActionResult> UpdateProgress(int id)  // ✅ Change from quoteId to id
        {
            var userId = int.Parse(User.FindFirstValue("UserId"));
            var vendor = await _context.Vendors
                .Include(v => v.User)
                .FirstOrDefaultAsync(v => v.UserId == userId);

            if (vendor == null)
            {
                TempData["Error"] = "❌ Vendor account not found.";
                return RedirectToAction("Dashboard");
            }

            var project = await _context.Quotes
                .Include(q => q.Request)
                    .ThenInclude(r => r.Client)
                        .ThenInclude(c => c.User)
                .Include(q => q.Vendor)
                    .ThenInclude(v => v.User)
                .FirstOrDefaultAsync(q => q.Id == id && q.VendorId == vendor.Id);  // ✅ Change to id

            if (project == null)
            {
                TempData["Error"] = $"❌ Project not found. Quote ID {id} does not belong to your account.";
                return RedirectToAction("MyProjects");
            }

            if (project.Status != "Accepted")
            {
                TempData["Error"] = $"❌ This quote is {project.Status}, not Accepted.";
                return RedirectToAction("MyProjects");
            }

            // Payment gates...
            if (!project.DepositPaid)
            {
                TempData["Error"] = "⛔ Cannot update progress. Client must pay deposit first.";
                return RedirectToAction("MyProjects");
            }

            return View(project);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProgress(int quoteId, string projectStatus, int progressPercentage, string vendorNotes)
        {
            var userId = int.Parse(User.FindFirstValue("UserId"));
            var vendor = await _context.Vendors
                .Include(v => v.User)
                .FirstOrDefaultAsync(v => v.UserId == userId);

            if (vendor == null)
            {
                TempData["Error"] = "❌ Vendor account not found.";
                return RedirectToAction("Dashboard");
            }

            var project = await _context.Quotes
                .Include(q => q.Request)
                    .ThenInclude(r => r.Client)
                        .ThenInclude(c => c.User)
                .Include(q => q.Vendor)
                .FirstOrDefaultAsync(q => q.Id == quoteId && q.VendorId == vendor.Id);

            if (project == null)
            {
                TempData["Error"] = $"❌ Project not found. Quote ID {quoteId} does not belong to your account.";
                return RedirectToAction("MyProjects");
            }

            // ========== PAYMENT VERIFICATION LOGIC ==========

            if (!project.DepositPaid)
            {
                TempData["Error"] = "❌ Cannot start project. Client must pay the deposit first ($" + project.DepositAmount.ToString("N2") + ").";
                return RedirectToAction("MyProjects");
            }

            if (progressPercentage > 20 && !project.DesignPaid)
            {
                TempData["Error"] = "❌ Cannot proceed past Design phase (20%). Waiting for client to pay Design milestone ($" + project.DesignPayment.ToString("N2") + ").";
                return RedirectToAction("MyProjects");
            }

            if (progressPercentage > 40 && !project.ProcurementPaid)
            {
                TempData["Error"] = "❌ Cannot proceed past Procurement phase (40%). Waiting for client to pay Procurement milestone ($" + project.ProcurementPayment.ToString("N2") + ").";
                return RedirectToAction("MyProjects");
            }

            if (progressPercentage > 80 && !project.InstallationPaid)
            {
                TempData["Error"] = "❌ Cannot proceed past Installation phase (80%). Waiting for client to pay Installation milestone ($" + project.InstallationPayment.ToString("N2") + ").";
                return RedirectToAction("MyProjects");
            }

            if (progressPercentage >= 100 && !project.InspectionPaid)
            {
                TempData["Error"] = "❌ Cannot mark project as complete (100%). Waiting for client to pay final Inspection milestone ($" + project.InspectionPayment.ToString("N2") + ").";
                return RedirectToAction("MyProjects");
            }

            // ========== UPDATE PROJECT PROGRESS ==========

            project.ProjectStatus = projectStatus;
            project.ProgressPercentage = progressPercentage;
            project.VendorNotes = vendorNotes;

            if (projectStatus != "Pending Start" && project.ProjectStartDate == null)
            {
                project.ProjectStartDate = DateTime.UtcNow;
            }

            // ✅ TRACK ON-TIME COMPLETION
            if (progressPercentage >= 100)
            {
                project.ProjectStatus = "Completed";
                project.ProgressPercentage = 100;

                if (project.ProjectCompletionDate == null)
                {
                    project.ProjectCompletionDate = DateTime.UtcNow;
                    project.ActualCompletionDate = DateTime.UtcNow;

                    // Check if completed on time
                    if (project.ProjectDeadline.HasValue)
                    {
                        var effectiveDeadline = project.EffectiveDeadline;
                        project.CompletedOnTime = project.ActualCompletionDate <= effectiveDeadline;
                    }
                    else
                    {
                        project.CompletedOnTime = true; // No deadline set
                    }

                    // Update vendor stats
                    if (vendor != null)
                    {
                        vendor.TotalProjectsCompleted = (vendor.TotalProjectsCompleted ?? 0) + 1;
                        if (project.CompletedOnTime)
                        {
                            vendor.ProjectsCompletedOnTime = (vendor.ProjectsCompletedOnTime ?? 0) + 1;
                        }
                    }
                }
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = "✅ Project progress updated successfully!";
            return RedirectToAction("MyProjects");
        }
        // ============================================================================
        // HELPER METHOD - Make sure this exists in your VendorController
        // ============================================================================
        // TEMPORARY - FOR TESTING ONLY
        [HttpGet]
        public async Task<IActionResult> SetDeadlineYesterday(int id)
        {
            var quote = await _context.Quotes.FindAsync(id);
            if (quote != null)
            {
                quote.ProjectDeadline = DateTime.UtcNow.AddDays(-2); // 2 days ago
                await _context.SaveChangesAsync();
                TempData["Success"] = "✅ Deadline set to 2 days ago for testing!";
            }
            return RedirectToAction("MyProjects");
        }
        private void UpdateOverdueStatus(Quote quote)
        {
            // Skip if project is completed
            if (quote.ProgressPercentage >= 100)
            {
                quote.IsOverdue = false;
                quote.DaysOverdue = 0;
                return;
            }

            // Skip if no deadline set
            if (!quote.ProjectDeadline.HasValue)
            {
                return;
            }

            var today = DateTime.UtcNow.Date;
            var effectiveDeadline = quote.EffectiveDeadline.Date;

            if (today > effectiveDeadline)
            {
                quote.IsOverdue = true;
                quote.DaysOverdue = (today - effectiveDeadline).Days;

                // Calculate penalty only if enabled and not waived
                if (quote.PenaltyEnabled && !quote.PenaltyWaivedByClient)
                {
                    // Don't calculate penalty if extension is pending approval
                    if (!quote.ExtensionRequested || quote.ExtensionApproved == false)
                    {
                        if (!quote.PenaltyStartDate.HasValue)
                        {
                            quote.PenaltyStartDate = effectiveDeadline.AddDays(1);
                        }

                        quote.AccumulatedPenalty = Math.Round(
                            quote.TotalPrice * (quote.PenaltyRate / 100) * quote.DaysOverdue,
                            2
                        );
                    }
                }
            }
            else
            {
                quote.IsOverdue = false;
                quote.DaysOverdue = 0;
            }
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
        // ============================================
        // ADD TO VendorController.cs
        // ============================================

        // GET: Request Extension
      

        // ============================================
        // ADD TO ClientController.cs
        // ============================================

        // GET: Review Extension Request
        public async Task<IActionResult> ReviewExtension(int quoteId)
        {
            var clientId = int.Parse(User.FindFirstValue("ClientId"));

            var project = await _context.Quotes
                .Include(q => q.Request)
                .Include(q => q.Vendor)
                    .ThenInclude(v => v.User)
                .FirstOrDefaultAsync(q => q.Id == quoteId && q.Request.ClientId == clientId);

            if (project == null) return NotFound();

            if (!project.ExtensionRequested)
            {
                TempData["Error"] = "No extension request pending for this project";
                return RedirectToAction("MyProjects");
            }

            return View(project);
        }

        // POST: Approve/Deny Extension
        [HttpPost]
        public async Task<IActionResult> ProcessExtension(int quoteId, bool approve, bool waivePenalties = false)
        {
            var clientId = int.Parse(User.FindFirstValue("ClientId"));

            var project = await _context.Quotes
                .Include(q => q.Request)
                .Include(q => q.Vendor)
                    .ThenInclude(v => v.User)
                .FirstOrDefaultAsync(q => q.Id == quoteId && q.Request.ClientId == clientId);

            if (project == null) return NotFound();

            var previousPenalty = project.AccumulatedPenalty; // Store for email

            project.ExtensionApproved = approve;
            project.ExtensionApprovedDate = DateTime.UtcNow;

            if (approve)
            {
                // Grant extension
                project.TotalExtensionDaysGranted += project.ExtensionDaysRequested;
                project.IsOverdue = false; // Reset overdue status
                project.DaysOverdue = 0;

                // Waive penalties if client chooses
                if (waivePenalties && project.AccumulatedPenalty > 0)
                {
                    project.PenaltyWaivedByClient = true;
                    project.PenaltyWaivedDate = DateTime.UtcNow;
                    project.PenaltyWaiverReason = "Extension approved - penalties waived";
                    project.AccumulatedPenalty = 0; // Reset penalty
                }

                TempData["Success"] = $"Extension approved! New deadline: {project.EffectiveDeadline:MMM dd, yyyy}";
            }
            else
            {
                // Deny extension - penalties continue
                TempData["Warning"] = "Extension denied. Penalties will continue to accumulate.";
            }

            project.ExtensionRequested = false; // Clear pending request

            await _context.SaveChangesAsync();

            // Notify vendor
            var vendorEmail = project.Vendor.User.Email;

            try
            {
                if (approve)
                {
                    await _emailService.SendEmailAsync(
                        vendorEmail,
                        "✅ Extension Approved",
                        $@"
                <div style='font-family: Arial, sans-serif;'>
                    <div style='background: linear-gradient(135deg, #27ae60, #229954); padding: 30px; text-align: center;'>
                        <h1 style='color: white; margin: 0;'>✅ Extension Approved</h1>
                    </div>
                    
                    <div style='background: #fff; padding: 30px;'>
                        <h2 style='color: #27ae60;'>Great News!</h2>
                        <p>Your extension request for <strong>{project.Request.PropertyAddress}</strong> has been approved.</p>
                        
                        <div style='background: #e8f5e9; padding: 20px; border-left: 4px solid #27ae60; margin: 20px 0;'>
                            <h3 style='margin-top: 0;'>New Timeline</h3>
                            <p style='margin: 5px 0;'><strong>Extension Granted:</strong> {project.ExtensionDaysRequested} days</p>
                            <p style='margin: 5px 0;'><strong>New Deadline:</strong> {project.EffectiveDeadline:MMM dd, yyyy}</p>
                            <p style='margin: 5px 0;'><strong>Current Progress:</strong> {project.ProgressPercentage}%</p>
                        </div>

                        {(waivePenalties && previousPenalty > 0 ? $@"
                        <div style='background: #d4edda; padding: 20px; border-left: 4px solid #28a745; margin: 20px 0;'>
                            <h3 style='margin-top: 0; color: #155724;'>💰 Penalties Waived</h3>
                            <p style='margin: 0; color: #155724;'>The client has waived the accumulated penalty of ${previousPenalty:N2}. Your full payment amounts are restored.</p>
                        </div>
                        " : "")}

                        <p>Please ensure completion by the new deadline to maintain client satisfaction.</p>
                    </div>
                </div>
                "
                    );
                }
                else
                {
                    var penaltyPerDay = project.TotalPrice * (project.PenaltyRate / 100m);

                    await _emailService.SendEmailAsync(
                        vendorEmail,
                        "❌ Extension Request Denied",
                        $@"
                <div style='font-family: Arial, sans-serif;'>
                    <div style='background: linear-gradient(135deg, #e74c3c, #c0392b); padding: 30px; text-align: center;'>
                        <h1 style='color: white; margin: 0;'>❌ Extension Denied</h1>
                    </div>
                    
                    <div style='background: #fff; padding: 30px;'>
                        <h2 style='color: #e74c3c;'>Extension Request Not Approved</h2>
                        <p>Unfortunately, the client has denied your extension request for <strong>{project.Request.PropertyAddress}</strong>.</p>
                        
                        <div style='background: #fee; padding: 20px; border-left: 4px solid #e74c3c; margin: 20px 0;'>
                            <h3 style='margin-top: 0;'>⚠️ Penalties Continue</h3>
                            <p style='margin: 5px 0;'><strong>Daily Penalty:</strong> ${penaltyPerDay:N2}</p>
                            <p style='margin: 5px 0;'><strong>Current Penalty:</strong> ${project.AccumulatedPenalty:N2}</p>
                            <p style='margin: 5px 0;'><strong>Original Deadline:</strong> {project.ProjectDeadline:MMM dd, yyyy}</p>
                            <p style='margin: 5px 0;'><strong>Days Overdue:</strong> {project.DaysOverdue}</p>
                        </div>

                        <h3>What You Can Do:</h3>
                        <ol style='line-height: 1.8;'>
                            <li>Complete the project as soon as possible to minimize penalties</li>
                            <li>Contact the client to discuss concerns</li>
                            <li>Submit another extension request with stronger justification</li>
                        </ol>

                        <div style='background: #fff3cd; padding: 15px; border-left: 4px solid #f39c12; margin: 20px 0;'>
                            <p style='margin: 0;'>💡 <strong>Tip:</strong> Completing the project today will prevent further daily penalties.</p>
                        </div>
                    </div>
                </div>
                "
                    );
                }
            }
            catch (Exception ex)
            {
                // Log error but don't fail the process
                Console.WriteLine($"Email error: {ex.Message}");
            }

            return RedirectToAction("MyProjects");
        }
        // GET: Request Extension
        // GET: Request Extension
        public async Task<IActionResult> RequestExtension(int id)
        {
            var userId = int.Parse(User.FindFirstValue("UserId"));  // ✅ Use UserId
            var vendor = await _context.Vendors
                .FirstOrDefaultAsync(v => v.UserId == userId);  // ✅ Get vendor from UserId

            if (vendor == null)
            {
                TempData["Error"] = "Vendor not found.";
                return RedirectToAction("MyProjects");
            }

            var project = await _context.Quotes
                .Include(q => q.Request)
                .Include(q => q.Vendor)
                .FirstOrDefaultAsync(q => q.Id == id && q.VendorId == vendor.Id);

            if (project == null)
            {
                TempData["Error"] = "Project not found.";
                return RedirectToAction("MyProjects");
            }

            if (!project.ProjectDeadline.HasValue)
            {
                TempData["Error"] = "No deadline set for this project";
                return RedirectToAction("MyProjects");
            }

            if (project.ExtensionRequested)
            {
                TempData["Info"] = "Extension request already pending";
                return RedirectToAction("MyProjects");
            }

            return View(project);
        }

        // POST: Submit Extension Request
        [HttpPost]
        public async Task<IActionResult> RequestExtension(int quoteId, int extensionDays, string reason)
        {
            var userId = int.Parse(User.FindFirstValue("UserId"));  // ✅ Use UserId
            var vendor = await _context.Vendors
                .FirstOrDefaultAsync(v => v.UserId == userId);  // ✅ Get vendor from UserId

            if (vendor == null)
            {
                TempData["Error"] = "Vendor not found.";
                return RedirectToAction("MyProjects");
            }

            var project = await _context.Quotes
                .Include(q => q.Request)
                    .ThenInclude(r => r.Client)
                        .ThenInclude(c => c.User)
                .Include(q => q.Vendor)
                    .ThenInclude(v => v.User)
                .FirstOrDefaultAsync(q => q.Id == quoteId && q.VendorId == vendor.Id);

            if (project == null) return NotFound();

            // Validation
            if (extensionDays < 1 || extensionDays > 30)
            {
                TempData["Error"] = "Extension must be between 1 and 30 days";
                return RedirectToAction("RequestExtension", new { quoteId });
            }

            if (string.IsNullOrWhiteSpace(reason) || reason.Length < 20)
            {
                TempData["Error"] = "Please provide a detailed reason (minimum 20 characters)";
                return RedirectToAction("RequestExtension", new { quoteId });
            }

            // Save extension request
            project.ExtensionRequested = true;
            project.ExtensionReason = reason;
            project.ExtensionDaysRequested = extensionDays;
            project.ExtensionRequestedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // Calculate new deadline if approved
            var newDeadline = project.ProjectDeadline.Value
                .AddDays(project.TotalExtensionDaysGranted + extensionDays);

            // Notify client
            var clientEmail = project.Request.Client.User.Email;
            var penaltyPerDay = project.TotalPrice * (project.PenaltyRate / 100m);

            try
            {
                await _emailService.SendEmailAsync(
                    clientEmail,
                    "📅 Deadline Extension Request - Action Required",
                    $@"
            <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
                <div style='background: linear-gradient(135deg, #3498db, #2980b9); padding: 30px; text-align: center;'>
                    <h1 style='color: white; margin: 0;'>📅 Extension Request</h1>
                </div>
                
                <div style='background: #fff; padding: 30px; border: 1px solid #ddd;'>
                    <h2>Deadline Extension Requested</h2>
                    <p><strong>{project.Vendor.CompanyName}</strong> has requested additional time to complete your solar installation project.</p>
                    
                    <div style='background: #e3f2fd; padding: 20px; border-left: 4px solid #2196f3; margin: 20px 0;'>
                        <h3 style='margin-top: 0;'>Project Details</h3>
                        <p style='margin: 5px 0;'><strong>Property:</strong> {project.Request.PropertyAddress}</p>
                        <p style='margin: 5px 0;'><strong>Current Progress:</strong> {project.ProgressPercentage}%</p>
                        <p style='margin: 5px 0;'><strong>Original Deadline:</strong> {project.ProjectDeadline:MMM dd, yyyy}</p>
                        {(project.IsOverdue ? $"<p style='margin: 5px 0; color: #e74c3c;'><strong>Status:</strong> Currently {project.DaysOverdue} days overdue</p>" : "")}
                    </div>

                    <div style='background: #fff3cd; padding: 20px; border-left: 4px solid #f39c12; margin: 20px 0;'>
                        <h3 style='margin-top: 0;'>Extension Request</h3>
                        <p style='margin: 5px 0;'><strong>Additional Days:</strong> {extensionDays} days</p>
                        <p style='margin: 5px 0;'><strong>New Deadline:</strong> {newDeadline:MMM dd, yyyy}</p>
                        <p style='margin: 5px 0;'><strong>Reason:</strong></p>
                        <div style='background: white; padding: 15px; border: 1px solid #ddd; margin-top: 10px;'>
                            <p style='margin: 0;'>{reason}</p>
                        </div>
                    </div>

                    <h3>Your Decision</h3>
                    
                    <div style='background: #e8f5e9; padding: 15px; border-left: 4px solid #4caf50; margin: 15px 0;'>
                        <p style='margin: 0;'><strong>✅ Approve:</strong> Deadline extended, penalties stopped/waived</p>
                    </div>
                    
                    <div style='background: #fee; padding: 15px; border-left: 4px solid #e74c3c; margin: 15px 0;'>
                        <p style='margin: 0;'><strong>❌ Deny:</strong> Penalties continue at ${penaltyPerDay:N2}/day</p>
                    </div>

                    {(project.IsOverdue ? $@"
                    <div style='background: #fff3cd; padding: 15px; border-left: 4px solid #f39c12; margin: 15px 0;'>
                        <p style='margin: 0;'><strong>Current Penalty:</strong> ${project.AccumulatedPenalty:N2}</p>
                        <p style='margin: 10px 0 0 0; font-size: 0.9em;'>If you approve, you can choose to waive accumulated penalties.</p>
                    </div>
                    " : "")}

                    <div style='text-align: center; margin: 30px 0;'>
                        <p style='margin: 0;'>Please review this request in your dashboard.</p>
                    </div>
                </div>
            </div>
            "
                );
            }
            catch (Exception ex)
            {
                // Log error but don't fail the request
                Console.WriteLine($"Email error: {ex.Message}");
            }

            TempData["Success"] = "Extension request sent to client. You'll be notified of their decision.";
            return RedirectToAction("MyProjects");
        }
        [HttpGet]
        public async Task<IActionResult> DebugProjects()
        {
            var userId = int.Parse(User.FindFirstValue("UserId"));
            var vendor = await _context.Vendors.FirstOrDefaultAsync(v => v.UserId == userId);

            var allQuotes = await _context.Quotes
                .Include(q => q.Request)
                .Where(q => q.VendorId == vendor.Id)
                .ToListAsync();

            var debug = allQuotes.Select(q => new
            {
                QuoteId = q.Id,
                RequestId = q.RequestId,
                Status = q.Status,
                Progress = q.ProgressPercentage,
                ClientEmail = q.Request?.Client?.User?.Email ?? "NULL"
            }).ToList();

            return Json(debug);
        }

    }
}