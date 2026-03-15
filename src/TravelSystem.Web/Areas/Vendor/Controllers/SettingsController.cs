using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TravelSystem.Shared.Models;

namespace TravelSystem.Web.Areas.Vendor.Controllers
{
    [Area("Vendor")]
    [Authorize(AuthenticationSchemes = "VendorAuth", Roles = "Vendor")]
    public class SettingsController : Controller
    {
        // TODO: Inject DbContext and UserService to get current vendor's shop
        
        public IActionResult Index()
        {
            // Load shop data from database (mock data for now)
            var shop = new Shop
            {
                Id = 1,
                Name = "Sahara Beer Club",
                Address = "123 Bùi Viện, District 1, HCM",
                PhoneNumber = "+84 28 1234 5678",
                ImageUrl = null
            };

            // Load shop hours from database
            var hours = new List<ShopHour>
            {
                new ShopHour { DayOfWeek = 1, OpenTime = TimeSpan.Parse("16:00"), CloseTime = TimeSpan.Parse("02:00") }, // Monday
                new ShopHour { DayOfWeek = 2, OpenTime = TimeSpan.Parse("16:00"), CloseTime = TimeSpan.Parse("02:00") }, // Tue
                new ShopHour { DayOfWeek = 3, OpenTime = TimeSpan.Parse("16:00"), CloseTime = TimeSpan.Parse("02:00") }, // Wed
                new ShopHour { DayOfWeek = 4, OpenTime = TimeSpan.Parse("16:00"), CloseTime = TimeSpan.Parse("02:00") }, // Thu
                new ShopHour { DayOfWeek = 5, OpenTime = TimeSpan.Parse("12:00"), CloseTime = TimeSpan.Parse("04:00") }, // Fri
                new ShopHour { DayOfWeek = 6, OpenTime = TimeSpan.Parse("12:00"), CloseTime = TimeSpan.Parse("04:00") }, // Sat
                new ShopHour { DayOfWeek = 0, OpenTime = TimeSpan.Parse("12:00"), CloseTime = TimeSpan.Parse("04:00") }, // Sun
            };

            ViewBag.Shop = shop;
            ViewBag.Hours = hours;
            return View();
        }

        [HttpPost]
        public IActionResult Index(Shop shop, List<ShopHour> hours)
        {
            // TODO: Save shop data (name, description, latitude, longitude, image)
            // TODO: Save shop hours
            // TODO: Handle file uploads for logo and gallery
            
            return RedirectToAction("Index");
        }
    }
}
