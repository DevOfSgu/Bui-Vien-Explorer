using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TravelSystem.Web.Data;

namespace TravelSystem.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(AuthenticationSchemes = "AdminAuth", Roles = "Admin")]
    public class VisitLogsController : Controller
    {
        private readonly AppDbContext _db;

        public VisitLogsController(AppDbContext db)
        {
            _db = db;
        }

        public async Task<IActionResult> Index()
        {
            var logs = await _db.Analytics.OrderByDescending(x => x.CreatedAt).Take(50).ToListAsync();
            return View(logs);
        }
    }
}
