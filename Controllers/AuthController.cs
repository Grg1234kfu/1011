using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SolarConnect.Data;
using SolarConnect.Models;
using SolarConnect.Models.ViewModels;

namespace SolarConnect.Controllers
{
    public class AuthController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AuthController(ApplicationDbContext context)
        {
            _context = context;
        }

        private string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                return Convert.ToBase64String(hashedBytes);
            }
        }

        private bool VerifyPassword(string password, string hashedPassword)
        {
            return HashPassword(password) == hashedPassword;
        }

        // GET: Login
        [HttpGet]
        public IActionResult Login()
        {
            if (User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }
            return View();
        }

        // POST: Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == model.Email);

            if (user != null && VerifyPassword(model.Password, user.Password))
            {
                if (!user.IsActive)
                {
                    ModelState.AddModelError(string.Empty, "Your account has been disabled.");
                    return View(model);
                }

                // Check vendor approval
                if (user.Role == "Vendor")
                {
                    var vendor = await _context.Vendors.FirstOrDefaultAsync(v => v.UserId == user.Id);
                    if (vendor != null && !vendor.IsApproved)
                    {
                        ModelState.AddModelError(string.Empty, "Your vendor account is pending approval.");
                        return View(model);
                    }
                }

                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, user.Email),
                    new Claim(ClaimTypes.Role, user.Role),
                    new Claim("UserId", user.Id.ToString())
                };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var authProperties = new AuthenticationProperties
                {
                    IsPersistent = model.RememberMe
                };

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity),
                    authProperties);

                // Redirect based on role
                if (user.Role == "Admin")
                {
                    return RedirectToAction("Dashboard", "Admin");
                }
                else if (user.Role == "Client")
                {
                    return RedirectToAction("Dashboard", "Client");
                }
                else if (user.Role == "Vendor")
                {
                    return RedirectToAction("Dashboard", "Vendor");
                }

                return RedirectToAction("Index", "Home");
            }

            ModelState.AddModelError(string.Empty, "Invalid email or password.");
            return View(model);
        }

        // GET: Register
        [HttpGet]
        public IActionResult Register()
        {
            if (User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }
            return View();
        }

        // GET: RegisterClient
        [HttpGet]
        public IActionResult RegisterClient()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegisterClient(ClientRegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Check if email already exists
            if (await _context.Users.AnyAsync(u => u.Email == model.Email))
            {
                ModelState.AddModelError("Email", "This email is already in use.");
                return View(model);
            }

            // ✅ Create the User account
            var user = new User
            {
                Email = model.Email,
                Password = BCrypt.Net.BCrypt.HashPassword(model.Password),
                FirstName = model.FirstName,
                LastName = model.LastName,
                PhoneNumber = model.PhoneNumber,
                Role = "Client",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // ✅ Create the Client profile (minimal)
            var client = new Client
            {
                UserId = user.Id,
                Address = string.Empty,
                PropertyType = string.Empty,
                RoofArea = 0,
                MonthlyConsumption = 0,
                MonthlyElectricityBill = 0,
                AdditionalNotes = string.Empty, // important fix
                CreatedAt = DateTime.UtcNow
            };

            _context.Clients.Add(client);
            await _context.SaveChangesAsync();

            // ✅ Auto login after registration
            var claims = new List<Claim>
    {
        new Claim(ClaimTypes.Name, user.Email),
        new Claim(ClaimTypes.Role, "Client"),
        new Claim("UserId", user.Id.ToString())
    };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity)
            );

            TempData["Success"] = "🎉 Your account has been created successfully!";
            return RedirectToAction("Dashboard", "Client");
        }

        // GET: RegisterVendor
        [HttpGet]
        public IActionResult RegisterVendor()
        {
            return View();
        }

        // POST: RegisterVendor
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegisterVendor(VendorRegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (await _context.Users.AnyAsync(u => u.Email == model.Email))
            {
                ModelState.AddModelError("Email", "This email is already in use.");
                return View(model);
            }

            var user = new User
            {
                Email = model.Email,
                Password = HashPassword(model.Password),
                FirstName = model.FirstName,
                LastName = model.LastName,
                PhoneNumber = model.PhoneNumber,
                Role = "Vendor",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var vendor = new Vendor
            {
                UserId = user.Id,
                CompanyName = model.CompanyName,
                BusinessLicense = model.BusinessLicense,
                YearsOfExperience = model.YearsOfExperience,
                ServiceArea = model.ServiceArea,
                Specialization = model.Specialization ?? "",
                Website = model.Website ?? "",
                CompanyDescription = model.CompanyDescription ?? "",
                IsApproved = false,
                Rating = 0,
                CreatedAt = DateTime.UtcNow
            };

            _context.Vendors.Add(vendor);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Your vendor account has been created. Please wait for admin approval.";
            return RedirectToAction("Login");
        }

        // POST: Logout
        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }
        [HttpGet]
        public async Task<IActionResult> CreateAdmin()
        {
            // Check if admin already exists
            if (await _context.Users.AnyAsync(u => u.Email == "admin@solarconnect.com"))
            {
                return Content("Admin already exists!");
            }

            var admin = new User
            {
                Email = "admin@solarconnect.com",
                Password = HashPassword("Admin123"),
                FirstName = "Admin",
                LastName = "User",
                PhoneNumber = "1234567890",
                Role = "Admin",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(admin);
            await _context.SaveChangesAsync();

            return Content("Admin user created! Email: admin@solarconnect.com, Password: Admin123");
        }
        [HttpGet]
        public async Task<IActionResult> CheckAdmin()
        {
            var admin = await _context.Users.FirstOrDefaultAsync(u => u.Email == "admin@solarconnect.com");

            if (admin == null)
            {
                return Content("❌ Admin user NOT found in database!");
            }

            // Test password
            var testPassword = HashPassword("Admin123");
            var passwordMatches = (testPassword == admin.Password);

            return Content($"✅ Admin found! Email: {admin.Email}, Role: {admin.Role}, Password matches: {passwordMatches}");
        }
        [HttpGet]
        public async Task<IActionResult> CreateTestAdmin()
        {
            // Delete old admin if exists
            var oldAdmin = await _context.Users.FirstOrDefaultAsync(u => u.Email == "mahmoud@solarconnet.com");
            if (oldAdmin != null)
            {
                _context.Users.Remove(oldAdmin);
                await _context.SaveChangesAsync();
            }

            // Create fresh admin
            var admin = new User
            {
                Email = "mahmoud@solarconnect.com",
                Password = HashPassword("Admin123"),
                FirstName = "Admin",
                LastName = "User",
                PhoneNumber = "1234567890",
                Role = "Admin",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(admin);
            await _context.SaveChangesAsync();

            return Content("✅ Fresh admin created! Try logging in now with mahmoud@solarconnect.com / Admin123");
        }
    }

}