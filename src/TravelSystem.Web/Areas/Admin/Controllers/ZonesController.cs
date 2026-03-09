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
    }
}
