using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TravelSystem.Web.Data;

namespace TravelSystem.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ZonesController : ControllerBase
{
    private readonly AppDbContext _db;

    public ZonesController(AppDbContext db)
    {
        _db = db;
    }

    // GET /api/zones?routeId=1
    // Lấy danh sách zones của 1 route (loại bỏ zones bị ẩn)
    [HttpGet]
    public async Task<IActionResult> GetByRoute([FromQuery] int routeId)
    {
        var zones = await _db.Zones
            .Where(z => z.RouteId == routeId && z.IsActive && !z.IsHidden)
            .OrderBy(z => z.OrderIndex)
            .ToListAsync();
        return Ok(zones);
    }

    // GET /api/zones/{id}
    // Lấy 1 zone theo ID
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var zone = await _db.Zones.FindAsync(id);
        if (zone == null || zone.IsHidden) return NotFound(new { message = $"Zone {id} không tồn tại." });
        return Ok(zone);
    }
}
