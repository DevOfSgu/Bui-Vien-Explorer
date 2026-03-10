using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TravelSystem.Shared.Models;
using TravelSystem.Web.Data;

namespace TravelSystem.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(AuthenticationSchemes = "AdminAuth", Roles = "Admin")]
    public class UsersController : Controller
    {
        private readonly AppDbContext _db;
        public UsersController(AppDbContext db)
        {
            _db = db;
        }

        // GET: Admin/Users
        public async Task<IActionResult> Index()
        {
            var users = await _db.Users.Include(u => u.Shop).ToListAsync();
            return View(users);
        }

        // GET: Admin/Users/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Admin/Users/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(User model, string password, string confirmPassword)
        {
            if (password != confirmPassword)
            {
                ModelState.AddModelError("", "Passwords do not match.");
            }

            if (ModelState.IsValid)
            {
                // very basic password storage for demo; in real app use hashing
                model.PasswordHash = password;
                model.CreatedAt = DateTime.UtcNow;
                _db.Users.Add(model);
                await _db.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }

        // POST: Admin/Users/Lock/5
        [HttpPost]
        public async Task<IActionResult> Lock(int id)
        {
            var user = await _db.Users.FindAsync(id);
            if (user != null)
            {
                user.IsActive = false;
                await _db.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        // POST: Admin/Users/Unlock/5
        [HttpPost]
        public async Task<IActionResult> Unlock(int id)
        {
            var user = await _db.Users.FindAsync(id);
            if (user != null)
            {
                user.IsActive = true;
                await _db.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}