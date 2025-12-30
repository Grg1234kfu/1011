using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SolarConnect.Data;
using SolarConnect.Services;
using SolarConnect.Models;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace SolarConnect.Controllers
{
    public class PaymentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IPaymentService _paymentService;
        private readonly StripeSettings _stripeSettings;

        public PaymentController(ApplicationDbContext context, IPaymentService paymentService, 
            IOptions<StripeSettings> stripeSettings)
        {
            _context = context;
            _paymentService = paymentService;
            _stripeSettings = stripeSettings.Value;
        }

        // Show payment page for specific milestone
        public async Task<IActionResult> PayMilestone(int quoteId, string milestone)
        {
            var quote = await _context.Quotes
                .Include(q => q.Request)
                    .ThenInclude(r => r.Client)
                        .ThenInclude(c => c.User)
                .Include(q => q.Vendor)
                    .ThenInclude(v => v.User)
                .FirstOrDefaultAsync(q => q.Id == quoteId);

            if (quote == null)
                return NotFound();

            // Check if user is the client who owns this quote
            var userId = int.Parse(User.FindFirstValue("UserId") ?? "0");
            if (quote.Request.Client.UserId != userId)
                return Forbid();

            // Determine payment amount and if already paid
            decimal amount = 0;
            bool alreadyPaid = false;
            string milestoneDescription = "";

            switch (milestone.ToLower())
            {
                case "deposit":
                    amount = quote.DepositAmount;
                    alreadyPaid = quote.DepositPaid;
                    milestoneDescription = "Deposit Payment (30%)";
                    break;
                case "design":
                    amount = quote.DesignPayment;
                    alreadyPaid = quote.DesignPaid;
                    milestoneDescription = "Design Phase Payment (15%)";
                    break;
                case "procurement":
                    amount = quote.ProcurementPayment;
                    alreadyPaid = quote.ProcurementPaid;
                    milestoneDescription = "Procurement Phase Payment (20%)";
                    break;
                case "installation":
                    amount = quote.InstallationPayment;
                    alreadyPaid = quote.InstallationPaid;
                    milestoneDescription = "Installation Phase Payment (25%)";
                    break;
                case "inspection":
                    amount = quote.InspectionPayment;
                    alreadyPaid = quote.InspectionPaid;
                    milestoneDescription = "Final Inspection Payment (10%)";
                    break;
                default:
                    return BadRequest("Invalid milestone");
            }

            if (alreadyPaid)
            {
                TempData["Error"] = "This milestone has already been paid.";
                return RedirectToAction("MyProjects", "Client");
            }

            ViewBag.QuoteId = quoteId;
            ViewBag.Milestone = milestone;
            ViewBag.Amount = amount;
            ViewBag.MilestoneDescription = milestoneDescription;
            ViewBag.StripePublishableKey = _stripeSettings.PublishableKey;
            Console.WriteLine($"DEBUG: Stripe Key = {_stripeSettings.PublishableKey}");
            ViewBag.VendorName = quote.Vendor.User.FirstName + " " + quote.Vendor.User.LastName;
            ViewBag.ProjectAddress = quote.Request.PropertyAddress;

            return View(quote);
        }

        // Create payment intent (called by JavaScript)
        [HttpPost]
        public async Task<IActionResult> CreatePaymentIntent([FromBody] PaymentIntentRequest request)
        {
            var quote = await _context.Quotes.FindAsync(request.QuoteId);
            if (quote == null)
                return NotFound();

            decimal amount = request.Milestone.ToLower() switch
            {
                "deposit" => quote.DepositAmount,
                "design" => quote.DesignPayment,
                "procurement" => quote.ProcurementPayment,
                "installation" => quote.InstallationPayment,
                "inspection" => quote.InspectionPayment,
                _ => 0
            };

            if (amount == 0)
                return BadRequest("Invalid amount");

            var description = $"SolarConnect - {request.Milestone} payment for Quote #{request.QuoteId}";
            var clientSecret = await _paymentService.CreatePaymentIntent(amount, "usd", description);

            return Json(new { clientSecret });
        }

        // Confirm payment after Stripe processes it
        [HttpPost]
        public async Task<IActionResult> ConfirmPayment([FromBody] ConfirmPaymentRequest request)
        {
            var quote = await _context.Quotes.FindAsync(request.QuoteId);
            if (quote == null)
                return NotFound();

            // Verify payment with Stripe
            bool paymentSucceeded = await _paymentService.VerifyPayment(request.PaymentIntentId);

            if (!paymentSucceeded)
                return BadRequest("Payment verification failed");

            // Update quote based on milestone
            switch (request.Milestone.ToLower())
            {
                case "deposit":
                    quote.DepositPaid = true;
                    quote.DepositPaidDate = DateTime.UtcNow;
                    quote.DepositStripePaymentId = request.PaymentIntentId;
                    quote.TotalPaidAmount += quote.DepositAmount;
                    quote.ProjectStatus = "Design Phase - Ready to Start";
                    break;
                case "design":
                    quote.DesignPaid = true;
                    quote.DesignPaidDate = DateTime.UtcNow;
                    quote.DesignStripePaymentId = request.PaymentIntentId;
                    quote.TotalPaidAmount += quote.DesignPayment;
                    quote.ProjectStatus = "Procurement Phase - Ready to Start";
                    break;
                case "procurement":
                    quote.ProcurementPaid = true;
                    quote.ProcurementPaidDate = DateTime.UtcNow;
                    quote.ProcurementStripePaymentId = request.PaymentIntentId;
                    quote.TotalPaidAmount += quote.ProcurementPayment;
                    quote.ProjectStatus = "Installation Phase - Ready to Start";
                    break;
                case "installation":
                    quote.InstallationPaid = true;
                    quote.InstallationPaidDate = DateTime.UtcNow;
                    quote.InstallationStripePaymentId = request.PaymentIntentId;
                    quote.TotalPaidAmount += quote.InstallationPayment;
                    quote.ProjectStatus = "Inspection Phase - Ready to Start";
                    break;
                case "inspection":
                    quote.InspectionPaid = true;
                    quote.InspectionPaidDate = DateTime.UtcNow;
                    quote.InspectionStripePaymentId = request.PaymentIntentId;
                    quote.TotalPaidAmount += quote.InspectionPayment;
                    quote.ProjectStatus = "Project Completed - All Payments Received";
                    break;
            }

            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }
    }

    // Helper classes for request bodies
    public class PaymentIntentRequest
    {
        public int QuoteId { get; set; }
        public string Milestone { get; set; } = string.Empty;
    }

    public class ConfirmPaymentRequest
    {
        public int QuoteId { get; set; }
        public string Milestone { get; set; } = string.Empty;
        public string PaymentIntentId { get; set; } = string.Empty;
    }
}