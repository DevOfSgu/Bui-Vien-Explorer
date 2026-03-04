using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TravelSystem.Web.Data;

namespace TravelSystem.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RoutesController : ControllerBase
{
    private readonly AppDbContext _db;

    public RoutesController(AppDbContext db)
    {
        _db = db;
    }

    // GET /api/routes
    // Lấy danh sách tất cả routes đang active
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var routes = await _db.Routes
            .Where(r => r.IsActive)
            .OrderBy(r => r.Id)
            .ToListAsync();
        return Ok(routes);
    }

    // GET /api/routes/{id}
    // Lấy 1 route theo ID, kèm danh sách zones và narrations (dùng khi Mobile scan QR)
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var route = await _db.Routes.FindAsync(id);
        if (route == null) return NotFound(new { message = $"Route {id} không tồn tại." });

        var zones = await _db.Zones
            .Where(z => z.RouteId == id && z.IsActive)
            .OrderBy(z => z.OrderIndex)
            .ToListAsync();

        var zoneIds = zones.Select(z => z.Id).ToList();
        var narrations = await _db.Narrations
            .Where(n => zoneIds.Contains(n.ZoneId))
            .ToListAsync();

        return Ok(new
        {
            route,
            zones,
            narrations
        });
    }
}