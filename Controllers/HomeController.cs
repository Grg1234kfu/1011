using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SolarConnect.Models;

namespace SolarConnect.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        // 🔹 Simulated in-memory storage for demo features (replace with DB later)
        private static List<dynamic> Reviews = new();
        private static List<dynamic> Requests = new();

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        // 🏠 Home Page
        public IActionResult Index() => View();

        // 📜 Privacy Page
        public IActionResult Privacy() => View();

        // ☀️ 1️⃣ Smart Energy Calculator
        [HttpGet]
        public IActionResult SmartEnergyCalculator()
        {
            return View();
        }

        [HttpPost]
        public IActionResult SmartEnergyCalculator(double RoofArea, double MonthlyConsumption, double SunHours)
        {
            // Basic calculation (approximation for Lebanon)
            double systemKW = Math.Round(MonthlyConsumption / (SunHours * 30) * 1.25, 2);
            double cost = Math.Round(systemKW * 700, 0);       // Average $/kW
            double savings = Math.Round(MonthlyConsumption * 0.85, 0);

            ViewBag.Result = new { kw = systemKW, cost, savings };
            return View();
        }

        // 🤝 2️⃣ Multiple Quotes
        [HttpGet]
        public IActionResult MultipleQuotes()
        {
            ViewBag.Requests = Requests;
            return View();
        }

        [HttpPost]
        public IActionResult MultipleQuotes(string SystemType, double RoofArea, double Budget, string City, DateTime PreferredDate)
        {
            var request = new
            {
                Id = Requests.Count + 1,
                SystemType,
                RoofArea,
                Budget,
                City,
                PreferredDate,
                CreatedAt = DateTime.UtcNow
            };

            Requests.Add(request);
            TempData["Success"] = $"Your {SystemType} request for {RoofArea}m² in {City} was submitted successfully!";
            return RedirectToAction("MultipleQuotes");
        }

        // 📈 3️⃣ Track Installation
        public IActionResult TrackInstallation()
        {
            // Simulated example project
            ViewBag.Status = "Panels Installed";
            ViewBag.Progress = 65; // percent
            return View();
        }

        // ⭐ 4️⃣ Verified Reviews
        [HttpGet]
        public IActionResult VerifiedReviews()
        {
            ViewBag.Reviews = Reviews;
            return View();
        }

        [HttpPost]
        public IActionResult VerifiedReviews(string VendorName, int Rating, string Comment)
        {
            Reviews.Add(new
            {
                Vendor = VendorName,
                Rating,
                Comment,
                Date = DateTime.UtcNow
            });

            ViewBag.Reviews = Reviews;
            return View();
        }

        // 💰 5️⃣ Transparent Pricing
        public IActionResult TransparentPricing()
        {
            // Static data displayed via Chart.js in the view
            return View();
        }

        // 🌿 6️⃣ Green Future
        public IActionResult GreenFuture()
        {
            // Simulated totals — can be computed from DB later
            double totalKW = 124.5; // Example total installed capacity
            double co2Saved = Math.Round(totalKW * 1.4, 2); // tons CO₂ saved per kW/year
            int trees = (int)(co2Saved * 45); // rough equivalence

            ViewBag.TotalKW = totalKW;
            ViewBag.CO2 = co2Saved;
            ViewBag.Trees = trees;
            return View();
        }

        // ⚙️ Error Handler
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
            });
        }
    }
}
