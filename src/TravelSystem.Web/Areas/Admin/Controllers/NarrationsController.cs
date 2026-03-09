using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TravelSystem.Web.Data;

namespace TravelSystem.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(AuthenticationSchemes = "AdminAuth", Roles = "Admin")]
    public class NarrationsController : Controller
    {
        private readonly AppDbContext _db;

        public NarrationsController(AppDbContext db)
        {
            _db = db;
        }

        public async Task<IActionResult> Index()
        {
            var narrations = await _db.Narrations
                .OrderBy(n => n.Id)
                .ToListAsync();

            return View(narrations);
        }
    }
}
