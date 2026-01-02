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
        private readonly IPaymentService _paymentService;
        private readonly IEmailService _emailService; // ADD THIS


        public ClientController(ApplicationDbContext context, IAIQuoteAdvisorService aiQuoteAdvisorService, IPaymentService paymentService, IEmailService emailService  )
        {
            _context = context;
            _aiQuoteAdvisorService = aiQuoteAdvisorService;
            _paymentService = paymentService;
            _emailService = emailService;
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

        // GET: My Projects - FIXED VERSION
        [HttpGet]
        [HttpGet]
        public async Task<IActionResult> MyProjects()
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue("UserId") ?? "0");
                var client = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == userId);
                if (client == null)
                    return RedirectToAction("Dashboard");

                // ✅ FIXED: Get quotes with Status = "Accepted" OR "Completed"
                var allProjects = await _context.Quotes
                    .Include(q => q.Request)
                        .ThenInclude(r => r.Client)
                        .ThenInclude(c => c.User)
                    .Include(q => q.Vendor)
                        .ThenInclude(v => v.User)
                    .Where(q => q.Request.ClientId == client.Id &&
                           (q.Status == "Accepted" || q.Status == "Completed")) // ← FIXED!
                    .OrderByDescending(q => q.SubmittedAt)
                    .ToListAsync();

                // Split into Active and Completed based on ProgressPercentage
                var activeProjects = allProjects
                    .Where(q => q.ProgressPercentage < 100)  // Less than 100% = Active
                    .ToList();

                var completedProjects = allProjects
                    .Where(q => q.ProgressPercentage >= 100)  // 100% or more = Completed
                    .ToList();

                // Pass both lists to the view using ViewBag
                ViewBag.ActiveProjects = activeProjects;
                ViewBag.CompletedProjects = completedProjects;

                return View();
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error: {ex.Message}";
                return RedirectToAction("Dashboard");
            }
        }
        // Add this to your ClientController.cs

        [HttpPost]
        public async Task<IActionResult> AcceptQuote(int quoteId)
        {
            var quote = await _context.Quotes
                .Include(q => q.Request)
                    .ThenInclude(r => r.Client)
                        .ThenInclude(c => c.User)
                .Include(q => q.Vendor)
                    .ThenInclude(v => v.User)
                .FirstOrDefaultAsync(q => q.Id == quoteId);

            if (quote == null) return NotFound();
            var userId = int.Parse(User.FindFirstValue("UserId"));
            var client = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == userId);
            if (client == null)
            {
                return NotFound();
            }
            // Calculate milestone payments
            _paymentService.CalculateMilestonePayments(
                quote.TotalPrice,
                out decimal deposit,
                out decimal design,
                out decimal procurement,
                out decimal installation,
                out decimal inspection
            );

            // ============================================
            // SET PROJECT DEADLINE
            // ============================================
            quote.ProjectStartDate = DateTime.UtcNow;
            quote.ProjectDeadline = DateTime.UtcNow.AddDays(quote.EstimatedInstallationDays);
            quote.PenaltyEnabled = true; // Enable penalties by default
            quote.PenaltyRate = 0.5m; // 0.5% per day

            quote.Status = "Accepted";
            quote.RespondedAt = DateTime.UtcNow;
            quote.ProjectStatus = "Pending";

            // Save payment amounts
            quote.DepositAmount = deposit;
            quote.DesignPayment = design;
            quote.ProcurementPayment = procurement;
            quote.InstallationPayment = installation;
            quote.InspectionPayment = inspection;

            // Mark request as closed
            quote.Request.Status = "Closed";
            quote.Request.ClosedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // ============================================
            // SEND CONFIRMATION EMAILS WITH DEADLINE
            // ============================================
            var clientEmail = quote.Request.Client.User.Email;
            var vendorEmail = quote.Vendor.User.Email;
            var penaltyPerDay = quote.TotalPrice * (quote.PenaltyRate / 100m);

            // Email to CLIENT
            try
            {
                await _emailService.SendEmailAsync(
                    clientEmail,
                    "✅ Quote Accepted - Project Started",
                    $@"
            <div style='font-family: Arial, sans-serif;'>
                <h2 style='color: #27ae60;'>Quote Accepted Successfully</h2>
                <p>You've accepted the quote from <strong>{quote.Vendor.CompanyName}</strong>.</p>
                
                <div style='background: #e8f5e9; padding: 20px; border-left: 4px solid #27ae60; margin: 20px 0;'>
                    <h3 style='margin-top: 0;'>Project Timeline</h3>
                    <p><strong>Start Date:</strong> {quote.ProjectStartDate:MMM dd, yyyy}</p>
                    <p><strong>Deadline:</strong> {quote.ProjectDeadline:MMM dd, yyyy}</p>
                    <p><strong>Duration:</strong> {quote.EstimatedInstallationDays} days</p>
                </div>

                <div style='background: #fff3cd; padding: 20px; border-left: 4px solid #f39c12; margin: 20px 0;'>
                    <h3 style='margin-top: 0;'>⚠️ Penalty System Active</h3>
                    <p>To protect your interests, automatic penalties apply if the vendor misses the deadline:</p>
                    <ul>
                        <li><strong>Penalty Rate:</strong> {quote.PenaltyRate}% per day</li>
                        <li><strong>Daily Amount:</strong> ${penaltyPerDay:N2}</li>
                        <li><strong>Deducted From:</strong> Final payment</li>
                    </ul>
                    <p><em>Vendor can request extensions for legitimate delays (weather, permits, etc.)</em></p>
                </div>

                <h3>Payment Schedule</h3>
                <table style='width: 100%; border-collapse: collapse;'>
                    <tr style='background: #f5f5f5;'>
                        <th style='padding: 10px; text-align: left; border: 1px solid #ddd;'>Milestone</th>
                        <th style='padding: 10px; text-align: right; border: 1px solid #ddd;'>Amount</th>
                    </tr>
                    <tr>
                        <td style='padding: 10px; border: 1px solid #ddd;'>Deposit (30%)</td>
                        <td style='padding: 10px; text-align: right; border: 1px solid #ddd;'>${deposit:N2}</td>
                    </tr>
                    <tr>
                        <td style='padding: 10px; border: 1px solid #ddd;'>Design (15%)</td>
                        <td style='padding: 10px; text-align: right; border: 1px solid #ddd;'>${design:N2}</td>
                    </tr>
                    <tr>
                        <td style='padding: 10px; border: 1px solid #ddd;'>Procurement (20%)</td>
                        <td style='padding: 10px; text-align: right; border: 1px solid #ddd;'>${procurement:N2}</td>
                    </tr>
                    <tr>
                        <td style='padding: 10px; border: 1px solid #ddd;'>Installation (25%)</td>
                        <td style='padding: 10px; text-align: right; border: 1px solid #ddd;'>${installation:N2}</td>
                    </tr>
                    <tr>
                        <td style='padding: 10px; border: 1px solid #ddd;'>Final Inspection (10%)</td>
                        <td style='padding: 10px; text-align: right; border: 1px solid #ddd;'>${inspection:N2}</td>
                    </tr>
                    <tr style='background: #f5f5f5; font-weight: bold;'>
                        <td style='padding: 10px; border: 1px solid #ddd;'>Total</td>
                        <td style='padding: 10px; text-align: right; border: 1px solid #ddd;'>${quote.TotalPrice:N2}</td>
                    </tr>
                </table>
            </div>
            "
                );
            }
            catch (Exception ex)
            {
                // Log but don't fail
                Console.WriteLine($"Email error to client: {ex.Message}");
            }

            // Email to VENDOR
            try
            {
                await _emailService.SendEmailAsync(
                    vendorEmail,
                    "🎉 Quote Accepted - Project Started",
                    $@"
            <div style='font-family: Arial, sans-serif;'>
                <h2 style='color: #27ae60;'>Congratulations! Quote Accepted</h2>
                <p>Your quote for <strong>{quote.Request.PropertyAddress}</strong> has been accepted.</p>
                
                <div style='background: #e8f5e9; padding: 20px; border-left: 4px solid #27ae60; margin: 20px 0;'>
                    <h3 style='margin-top: 0;'>Project Details</h3>
                    <p><strong>Total Contract:</strong> ${quote.TotalPrice:N2}</p>
                    <p><strong>Start Date:</strong> {quote.ProjectStartDate:MMM dd, yyyy}</p>
                    <p><strong>⚠️ Deadline:</strong> {quote.ProjectDeadline:MMM dd, yyyy}</p>
                    <p><strong>Duration:</strong> {quote.EstimatedInstallationDays} days</p>
                </div>

                <div style='background: #fff3cd; padding: 20px; border-left: 4px solid #f39c12; margin: 20px 0;'>
                    <h3 style='margin-top: 0;'>⚠️ Important: Deadline Penalties</h3>
                    <p>To ensure timely completion, the following penalty system is in effect:</p>
                    <ul>
                        <li><strong>Penalty Rate:</strong> {quote.PenaltyRate}% per day after deadline</li>
                        <li><strong>Daily Deduction:</strong> ${penaltyPerDay:N2}</li>
                        <li><strong>Applied To:</strong> Final payment (${inspection:N2})</li>
                    </ul>
                    <p><strong>💡 How to avoid penalties:</strong></p>
                    <ol>
                        <li>Complete the project by {quote.ProjectDeadline:MMM dd, yyyy}</li>
                        <li>Request an extension BEFORE the deadline if legitimate delays occur</li>
                        <li>Keep the client updated on progress regularly</li>
                    </ol>
                </div>

                <div style='background: #e3f2fd; padding: 20px; border-left: 4px solid #2196f3; margin: 20px 0;'>
                    <h3 style='margin-top: 0;'>Next Steps</h3>
                    <ol>
                        <li>Wait for client to pay 30% deposit (${deposit:N2})</li>
                        <li>Once paid, you can start work and update progress</li>
                        <li>Update progress regularly to unlock milestone payments</li>
                    </ol>
                </div>
            </div>
            "
                );
            }
            catch (Exception ex)
            {
                // Log but don't fail
                Console.WriteLine($"Email error to vendor: {ex.Message}");
            }

            TempData["Success"] = $"Quote accepted! Project deadline: {quote.ProjectDeadline:MMM dd, yyyy}. Penalties apply for delays.";
            return RedirectToAction("MyProjects");
        }
       

        // POST: Approve/Deny Extension
      
        // GET: AI Quote Analysis
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
        // ============================================
        // ADD THESE METHODS TO YOUR ClientController.cs
        // Place them after the AIQuoteAnalysis method
        // ============================================

        // GET: Review Extension Request
        [HttpGet]
        public async Task<IActionResult> ReviewExtension(int quoteId)
        {
            var userId = int.Parse(User.FindFirstValue("UserId"));
            var client = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == userId);

            if (client == null)
            {
                return RedirectToAction("Dashboard");
            }

            var project = await _context.Quotes
                .Include(q => q.Request)
                .Include(q => q.Vendor)
                    .ThenInclude(v => v.User)
                .FirstOrDefaultAsync(q => q.Id == quoteId && q.Request.ClientId == client.Id);

            if (project == null)
            {
                TempData["Error"] = "Project not found";
                return RedirectToAction("MyProjects");
            }

            if (!project.ExtensionRequested)
            {
                TempData["Error"] = "No extension request pending for this project";
                return RedirectToAction("MyProjects");
            }

            return View(project);
        }

        // POST: Approve/Deny Extension
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessExtension(int quoteId, bool approve, bool waivePenalties = false)
        {
            var userId = int.Parse(User.FindFirstValue("UserId"));
            var client = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == userId);

            if (client == null)
            {
                return RedirectToAction("Dashboard");
            }

            var project = await _context.Quotes
                .Include(q => q.Request)
                .Include(q => q.Vendor)
                    .ThenInclude(v => v.User)
                .FirstOrDefaultAsync(q => q.Id == quoteId && q.Request.ClientId == client.Id);

            if (project == null)
            {
                TempData["Error"] = "Project not found";
                return RedirectToAction("MyProjects");
            }

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
        [HttpGet]
        public async Task<IActionResult> RequestExtension(int quoteId)
        {
            var userId = int.Parse(User.FindFirstValue("UserId"));  // ✅ FIXED: Use UserId
            var vendor = await _context.Vendors.FirstOrDefaultAsync(v => v.UserId == userId);

            if (vendor == null)
            {
                return NotFound();
            }

            var project = await _context.Quotes
                .Include(q => q.Request)
                .Include(q => q.Vendor)
                .FirstOrDefaultAsync(q => q.Id == quoteId && q.VendorId == vendor.Id);

            if (project == null) return NotFound();

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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestExtension(int quoteId, int extensionDays, string reason)
        {
            var userId = int.Parse(User.FindFirstValue("UserId"));  // ✅ FIXED: Use UserId
            var vendor = await _context.Vendors.FirstOrDefaultAsync(v => v.UserId == userId);

            if (vendor == null)
            {
                return NotFound();
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
                Console.WriteLine($"Email error: {ex.Message}");
            }

            TempData["Success"] = "Extension request sent to client. You'll be notified of their decision.";
            return RedirectToAction("MyProjects");
        }
        // POST: Rate Completed Project
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RateProject(int quoteId, int rating, string? review)
        {
            var userId = int.Parse(User.FindFirstValue("UserId"));
            var client = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == userId);

            if (client == null)
                return RedirectToAction("Dashboard");

            var project = await _context.Quotes
                .Include(q => q.Request)
                .FirstOrDefaultAsync(q =>
                    q.Id == quoteId &&
                    q.Request.ClientId == client.Id);

            if (project == null)
            {
                TempData["Error"] = "Project not found.";
                return RedirectToAction("MyProjects");
            }

            if (project.ProgressPercentage < 100)
            {
                TempData["Error"] = "You can only rate completed projects.";
                return RedirectToAction("MyProjects");
            }

            if (project.ClientRating != null)
            {
                TempData["Error"] = "This project has already been rated.";
                return RedirectToAction("MyProjects");
            }

            project.ClientRating = rating;
            project.ClientReview = review;
            project.RatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            TempData["Success"] = "Thank you for rating this project!";
            return RedirectToAction("MyProjects");
        }


    }
}