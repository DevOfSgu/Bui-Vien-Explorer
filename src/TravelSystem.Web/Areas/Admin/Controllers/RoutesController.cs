using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TravelSystem.Shared.Models;
using TravelSystem.Web.Data;

namespace TravelSystem.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(AuthenticationSchemes = "AdminAuth", Roles = "Admin")]
    public class RoutesController : Controller
    {
        private readonly AppDbContext _db;

        public RoutesController(AppDbContext db)
        {
            _db = db;
        }

        // GET: Admin/Routes
        public async Task<IActionResult> Index()
        {
            var routes = await _db.Routes.ToListAsync();

            // Count zones for each route
            ViewBag.ZoneCounts = await _db.Zones
                .GroupBy(z => z.RouteId)
                .ToDictionaryAsync(g => g.Key, g => g.Count());

            return View(routes);
        }

        // GET: Admin/Routes/Create
        public IActionResult Create()
        {
            return View(new Routes { IsActive = true });
        }

        // POST: Admin/Routes/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Routes route)
        {
            if (ModelState.IsValid)
            {
                route.CreatedAt = DateTime.UtcNow;
                route.UpdatedAt = DateTime.UtcNow;
                route.IsLocked = false;
                route.IsHidden = false;

                _db.Routes.Add(route);
                await _db.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(route);
        }

        // GET: Admin/Routes/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var route = await _db.Routes.FindAsync(id);
            if (route == null) return NotFound();
            
            if (route.IsLocked)
            {
                ModelState.AddModelError("", $"Route này đang bị khóa. Lý do: {route.LockReason}");
            }

            return View(route);
        }

        // POST: Admin/Routes/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Routes route)
        {
            if (id != route.Id) return NotFound();

            var dbRoute = await _db.Routes.FindAsync(id);
            if (dbRoute == null) return NotFound();

            // Kiểm tra nếu route bị khóa
            if (dbRoute.IsLocked)
            {
                ModelState.AddModelError("", $"Không thể chỉnh sửa. Route này đang bị khóa. Lý do: {dbRoute.LockReason}");
                return View(route);
            }

            if (ModelState.IsValid)
            {
                dbRoute.Name = route.Name;
                dbRoute.Description = route.Description;
                dbRoute.StartLatitude = route.StartLatitude;
                dbRoute.StartLongitude = route.StartLongitude;
                dbRoute.ImageUrl = route.ImageUrl;
                dbRoute.IsActive = route.IsActive;
                dbRoute.UpdatedAt = DateTime.UtcNow;

                await _db.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(route);
        }

        // POST: Admin/Routes/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var route = await _db.Routes.FindAsync(id);
            if (route != null)
            {
                _db.Routes.Remove(route);
                await _db.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        // POST: Admin/Routes/Lock/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Lock(int id, string reason = "")
        {
            var route = await _db.Routes.FindAsync(id);
            if (route != null)
            {
                route.IsLocked = true;
                route.LockReason = reason ?? "Lý do không được cung cấp";
                route.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        // POST: Admin/Routes/Unlock/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Unlock(int id)
        {
            var route = await _db.Routes.FindAsync(id);
            if (route != null)
            {
                route.IsLocked = false;
                route.LockReason = null;
                route.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        // POST: Admin/Routes/Hide/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Hide(int id)
        {
            var route = await _db.Routes.FindAsync(id);
            if (route != null)
            {
                route.IsHidden = true;
                route.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        // POST: Admin/Routes/Show/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Show(int id)
        {
            var route = await _db.Routes.FindAsync(id);
            if (route != null)
            {
                route.IsHidden = false;
                route.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}

