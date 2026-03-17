using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
            var vendor = await _db.Users.FirstOrDefaultAsync(u => u.Username == username);
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

                // Update hours (simple update)
                var existingHours = _db.ShopHours.Where(h => h.ShopId == dbShop.Id);
                _db.ShopHours.RemoveRange(existingHours);
                
                foreach(var h in hours)
                {
                    h.ShopId = dbShop.Id;
                    _db.ShopHours.Add(h);
                }

                await _db.SaveChangesAsync();
                TempData["Success"] = "Settings updated successfully.";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Shop = dbShop;
            ViewBag.Hours = hours;
            return View();
        }
    }
}
