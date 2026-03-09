using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

        public async Task<IActionResult> Index()
        {
            var routes = await _db.Routes.ToListAsync();

            // Count zones for each route
            ViewBag.ZoneCounts = await _db.Zones
                .GroupBy(z => z.RouteId)
                .ToDictionaryAsync(g => g.Key, g => g.Count());

            return View(routes);
        }
    }
}
