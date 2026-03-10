using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TravelSystem.Web.Areas.Admin.Models;
using Microsoft.EntityFrameworkCore;

namespace TravelSystem.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(AuthenticationSchemes = "AdminAuth", Roles = "Admin")]
    public class SettingsController : Controller
    {
        private readonly TravelSystem.Web.Data.AppDbContext _db;

        public SettingsController(TravelSystem.Web.Data.AppDbContext db)
        {
            _db = db;
        }

        public async Task<IActionResult> Index()
        {
            // load current admin profile and system preferences
            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username))
                return Challenge("AdminAuth");

            var admin = await _db.Users.FirstOrDefaultAsync(u => u.Username == username && u.Role == 0);
            if (admin == null)
                return NotFound();

            var vm = new AdminSettingsViewModel
            {
                FullName = admin.FullName,
                Email = admin.Email,
            };
            var lang = await _db.AppSettings.FindAsync("DefaultLanguage");
            var api = await _db.AppSettings.FindAsync("EnableApiSync");
            vm.DefaultLanguage = lang?.Value ?? "vi";
            vm.EnableApiSync = api != null && api.Value == "1";

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(AdminSettingsViewModel vm)
        {
            if (!ModelState.IsValid)
                return View(vm);

            var username = User.Identity?.Name;
            var admin = await _db.Users.FirstOrDefaultAsync(u => u.Username == username && u.Role == 0);
            if (admin == null)
                return NotFound();

            admin.FullName = vm.FullName;
            admin.Email = vm.Email;
            if (!string.IsNullOrEmpty(vm.Password))
            {
                admin.PasswordHash = vm.Password;
            }

            // settings
            var lang = await _db.AppSettings.FindAsync("DefaultLanguage");
            if (lang == null)
            {
                _db.AppSettings.Add(new TravelSystem.Shared.Models.AppSetting
                {
                    Key = "DefaultLanguage",
                    Value = vm.DefaultLanguage,
                    UpdatedAt = DateTime.UtcNow
                });
            }
            else
            {
                lang.Value = vm.DefaultLanguage;
                lang.UpdatedAt = DateTime.UtcNow;
            }

            var api = await _db.AppSettings.FindAsync("EnableApiSync");
            if (api == null)
            {
                _db.AppSettings.Add(new TravelSystem.Shared.Models.AppSetting
                {
                    Key = "EnableApiSync",
                    Value = vm.EnableApiSync ? "1" : "0",
                    UpdatedAt = DateTime.UtcNow
                });
            }
            else
            {
                api.Value = vm.EnableApiSync ? "1" : "0";
                api.UpdatedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();
            TempData["Success"] = "Settings updated successfully.";
            return RedirectToAction();
        }
    }
}
