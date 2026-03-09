using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TravelSystem.Web.Data;

namespace TravelSystem.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(AuthenticationSchemes = "AdminAuth", Roles = "Admin")]
    public class HeatmapController : Controller
    {
        private readonly AppDbContext _db;

        public HeatmapController(AppDbContext db)
        {
            _db = db;
        }

        public async Task<IActionResult> Index()
        {
            var data = await _db.Analytics
                .Select(a => new { a.Latitude, a.Longitude, a.ActionType })
                .ToListAsync();
            
            return View(data);
        }
    }
}
