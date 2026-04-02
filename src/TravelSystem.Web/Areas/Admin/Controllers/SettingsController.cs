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
            // load current admin profile
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
                admin.PasswordHash = Helpers.PasswordHelper.HashPassword(admin, vm.Password);
            }

            await _db.SaveChangesAsync();
            TempData["Success"] = "Cập nhật thông tin thành công.";
            return RedirectToAction();
        }
    }
}
