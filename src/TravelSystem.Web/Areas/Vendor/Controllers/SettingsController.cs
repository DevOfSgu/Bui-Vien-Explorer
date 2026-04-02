using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TravelSystem.Shared.Models;

namespace TravelSystem.Web.Areas.Vendor.Controllers
{
    [Area("Vendor")]
    [Authorize(AuthenticationSchemes = "VendorAuth", Roles = "Vendor")]
    public class SettingsController : Controller
    {
        private readonly TravelSystem.Web.Data.AppDbContext _db;
        private readonly IWebHostEnvironment _env;

        public SettingsController(TravelSystem.Web.Data.AppDbContext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }
        
        public async Task<IActionResult> Index()
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username)) return Challenge("VendorAuth");

            var vendor = await _db.Users.FirstOrDefaultAsync(u => u.Username == username && u.Role == 1);
            if (vendor == null) return NotFound();

            var shop = await _db.Shops.FirstOrDefaultAsync(s => s.Id == vendor.ShopId);
            if (shop == null) return NotFound();

            var hours = await _db.ShopHours.Where(h => h.ShopId == shop.Id).OrderBy(h => h.DayOfWeek).ToListAsync();

            ViewBag.Shop = shop;
            ViewBag.Hours = hours;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(Shop shop, List<ShopHour> hours, IFormFile? logoFile)
        {
            var username = User.Identity?.Name;
            var vendor = await _db.Users.FirstOrDefaultAsync(u => u.Username == username && u.Role == 1);
            if (vendor == null || vendor.ShopId == null) return NotFound();

            var dbShop = await _db.Shops.FindAsync(vendor.ShopId);
            if (dbShop == null) return NotFound();

            if (ModelState.IsValid)
            {
                // Basic Info
                dbShop.Name = shop.Name;
                dbShop.Address = shop.Address;
                dbShop.PhoneNumber = shop.PhoneNumber;

                // Handle Logo Upload
                if (logoFile != null)
                {
                    if (!string.IsNullOrEmpty(dbShop.ImageUrl))
                        Helpers.FileStorageHelper.DeleteImage(dbShop.ImageUrl, _env.WebRootPath);

                    dbShop.ImageUrl = await Helpers.FileStorageHelper.SaveImageAsync(logoFile, _env.WebRootPath, "vendors");
                }

                _db.Shops.Update(dbShop);

                // Update hours (UI sends 3 groups: Mon, Tue-Thu, Fri-Sun).
                // Expand these into concrete records for day 1..7 so downstream open/close logic stays correct.
                var normalizedHours = NormalizeHours(hours, dbShop.Id);

                var existingHours = _db.ShopHours.Where(h => h.ShopId == dbShop.Id);
                _db.ShopHours.RemoveRange(existingHours);

                foreach (var h in normalizedHours)
                {
                    _db.ShopHours.Add(h);
                }

                await _db.SaveChangesAsync();
                TempData["Success"] = "Settings updated successfully.";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Shop = dbShop;
            ViewBag.Hours = NormalizeHours(hours, dbShop.Id);
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword, string confirmPassword)
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(username))
            {
                return Challenge("VendorAuth");
            }

            var vendor = await _db.Users.FirstOrDefaultAsync(u => u.Username == username && u.Role == 1);
            if (vendor == null)
            {
                return NotFound();
            }

            if (string.IsNullOrWhiteSpace(currentPassword) || string.IsNullOrWhiteSpace(newPassword) || string.IsNullOrWhiteSpace(confirmPassword))
            {
                TempData["ChangePasswordError"] = "Please fill in all password fields.";
                TempData["OpenChangePasswordModal"] = "1";
                return RedirectToAction(nameof(Index));
            }

            if (!Helpers.PasswordHelper.VerifyPassword(vendor, currentPassword, out _))
            {
                TempData["ChangePasswordError"] = "Current password is incorrect.";
                TempData["OpenChangePasswordModal"] = "1";
                return RedirectToAction(nameof(Index));
            }

            if (!string.Equals(newPassword, confirmPassword, StringComparison.Ordinal))
            {
                TempData["ChangePasswordError"] = "New password and confirmation do not match.";
                TempData["OpenChangePasswordModal"] = "1";
                return RedirectToAction(nameof(Index));
            }

            if (newPassword.Length < 6)
            {
                TempData["ChangePasswordError"] = "New password must be at least 6 characters.";
                TempData["OpenChangePasswordModal"] = "1";
                return RedirectToAction(nameof(Index));
            }

            if (string.Equals(currentPassword, newPassword, StringComparison.Ordinal))
            {
                TempData["ChangePasswordError"] = "New password must be different from current password.";
                TempData["OpenChangePasswordModal"] = "1";
                return RedirectToAction(nameof(Index));
            }

            vendor.PasswordHash = Helpers.PasswordHelper.HashPassword(vendor, newPassword);
            _db.Users.Update(vendor);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Password changed successfully.";
            return RedirectToAction(nameof(Index));
        }

        private static List<ShopHour> NormalizeHours(IEnumerable<ShopHour>? inputHours, int shopId)
        {
            var source = (inputHours ?? Enumerable.Empty<ShopHour>())
                .Where(h => h != null)
                .GroupBy(h => h.DayOfWeek)
                .ToDictionary(g => g.Key, g => g.First());

            var byDay = new Dictionary<int, ShopHour>();

            void AddRangeFrom(int sourceDay, params int[] targetDays)
            {
                if (!source.TryGetValue(sourceDay, out var src))
                {
                    return;
                }

                foreach (var day in targetDays.Where(d => d is >= 1 and <= 7))
                {
                    byDay[day] = new ShopHour
                    {
                        ShopId = shopId,
                        DayOfWeek = day,
                        OpenTime = src.OpenTime,
                        CloseTime = src.CloseTime
                    };
                }
            }

            // Group mapping from UI.
            AddRangeFrom(1, 1);
            AddRangeFrom(2, 2, 3, 4);
            AddRangeFrom(5, 5, 6, 7);

            // Support direct per-day posting if needed in future UI.
            foreach (var day in source.Keys.Where(d => d is >= 1 and <= 7 && d != 1 && d != 2 && d != 5))
            {
                var src = source[day];
                byDay[day] = new ShopHour
                {
                    ShopId = shopId,
                    DayOfWeek = day,
                    OpenTime = src.OpenTime,
                    CloseTime = src.CloseTime
                };
            }

            return byDay.Values.OrderBy(h => h.DayOfWeek).ToList();
        }
    }
}
