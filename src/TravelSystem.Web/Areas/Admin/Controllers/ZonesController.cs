using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

        public async Task<IActionResult> Index(int? routeId)
        {
            var query = _db.Zones.AsQueryable();

            if (routeId.HasValue)
            {
                query = query.Where(z => z.RouteId == routeId.Value);
                ViewBag.SelectedRouteId = routeId;
            }

            var zones = await query
                .OrderBy(z => z.OrderIndex)
                .ToListAsync();

            // Count narrations for each zone dynamically just for view mockups
            ViewBag.NarrationCounts = await _db.Narrations
                .GroupBy(n => n.ZoneId)
                .ToDictionaryAsync(g => g.Key, g => g.Count());

            return View(zones);
        }

        // GET: Admin/Zones/Create
        public IActionResult Create(int? routeId)
        {
            ViewBag.Routes = _db.Routes.ToList();
            ViewBag.Shops = _db.Shops.ToList();
            return View(new TravelSystem.Shared.Models.Zone { RouteId = routeId ?? 0, IsActive = true });
        }

        // POST: Admin/Zones/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(TravelSystem.Shared.Models.Zone zone)
        {
            zone.CreatedAt = DateTime.UtcNow;
            zone.UpdatedAt = DateTime.UtcNow;
            
            _db.Zones.Add(zone);
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index), new { routeId = zone.RouteId });
        }

        // GET: Admin/Zones/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var zone = await _db.Zones.FindAsync(id);
            if (zone == null) return NotFound();

            ViewBag.Routes = _db.Routes.ToList();
            ViewBag.Shops = _db.Shops.ToList();
            return View(zone);
        }

        // POST: Admin/Zones/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, TravelSystem.Shared.Models.Zone zone)
        {
            if (id != zone.Id) return NotFound();

            var dbZone = await _db.Zones.FindAsync(id);
            if(dbZone != null) {
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
            }

            return RedirectToAction(nameof(Index), new { routeId = zone.RouteId });
        }
    }
}
