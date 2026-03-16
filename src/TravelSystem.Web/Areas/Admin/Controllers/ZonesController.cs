using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TravelSystem.Shared.Models;
using TravelSystem.Web.Data;

namespace TravelSystem.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(AuthenticationSchemes = "AdminAuth", Roles = "Admin")]
    public class ZonesController : Controller
    {
        private readonly AppDbContext _db;

        public ZonesController(AppDbContext db)
        {
            _db = db;
        }

        // GET: Admin/Zones
        public async Task<IActionResult> Index(int page = 1)
        {
            const int pageSize = 20;
            var query = _db.Zones.AsQueryable();

            var totalCount = await query.CountAsync();
            var totalPages = (int) Math.Ceiling(totalCount / (double)pageSize);
            ViewBag.Page = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalCount = totalCount;
            ViewBag.PageSize = pageSize;

            var zones = await query
                .OrderBy(z => z.OrderIndex)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Count narrations for each zone dynamically just for view mockups
            ViewBag.NarrationCounts = await _db.Narrations
                .GroupBy(n => n.ZoneId)
                .ToDictionaryAsync(g => g.Key, g => g.Count());

            return View(zones);
        }

        // GET: Admin/Zones/Create
        public IActionResult Create()
        {
            ViewBag.Shops = _db.Shops.ToList();
            return View(new Zone { IsActive = true });
        }

        // POST: Admin/Zones/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Zone zone)
        {
            if (ModelState.IsValid)
            {
                zone.CreatedAt = DateTime.UtcNow;
                zone.UpdatedAt = DateTime.UtcNow;
                zone.IsLocked = false;
                zone.IsHidden = false;

                _db.Zones.Add(zone);
                await _db.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Shops = _db.Shops.ToList();
            return View(zone);
        }

        // GET: Admin/Zones/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var zone = await _db.Zones.FindAsync(id);
            if (zone == null) return NotFound();

            if (zone.IsLocked)
            {
                ModelState.AddModelError("", $"Zone này đang bị khóa. Lý do: {zone.LockReason}");
            }

            ViewBag.Shops = _db.Shops.ToList();
            return View(zone);
        }

        // POST: Admin/Zones/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Zone zone)
        {
            if (id != zone.Id) return NotFound();

            var dbZone = await _db.Zones.FindAsync(id);
            if (dbZone == null) return NotFound();

            // Kiểm tra nếu zone bị khóa
            if (dbZone.IsLocked)
            {
                ModelState.AddModelError("", $"Không thể chỉnh sửa. Zone này đang bị khóa. Lý do: {dbZone.LockReason}");
                ViewBag.Shops = _db.Shops.ToList();
                return View(zone);
            }

            if (ModelState.IsValid)
            {
                dbZone.Name = zone.Name;
                dbZone.Description = zone.Description;
                dbZone.Latitude = zone.Latitude;
                dbZone.Longitude = zone.Longitude;
                dbZone.Radius = zone.Radius;
                dbZone.ZoneType = zone.ZoneType;
                dbZone.ShopId = zone.ShopId;
                dbZone.OrderIndex = zone.OrderIndex;
                dbZone.IsActive = zone.IsActive;
                dbZone.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Shops = _db.Shops.ToList();
            return View(zone);
        }

        // POST: Admin/Zones/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var zone = await _db.Zones.FindAsync(id);
            if (zone != null)
            {
                _db.Zones.Remove(zone);
                await _db.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return RedirectToAction(nameof(Index));
        }

        // POST: Admin/Zones/Lock/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Lock(int id, string reason = "")
        {
            var zone = await _db.Zones.FindAsync(id);
            if (zone != null)
            {
                zone.IsLocked = true;
                zone.LockReason = reason ?? "Lý do không được cung cấp";
                zone.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        // POST: Admin/Zones/Unlock/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Unlock(int id)
        {
            var zone = await _db.Zones.FindAsync(id);
            if (zone != null)
            {
                zone.IsLocked = false;
                zone.LockReason = null;
                zone.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        // POST: Admin/Zones/Hide/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Hide(int id)
        {
            var zone = await _db.Zones.FindAsync(id);
            if (zone != null)
            {
                zone.IsHidden = true;
                zone.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        // POST: Admin/Zones/Show/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Show(int id)
        {
            var zone = await _db.Zones.FindAsync(id);
            if (zone != null)
            {
                zone.IsHidden = false;
                zone.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}

