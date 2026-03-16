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
    // Lấy danh sách tất cả routes đang active (loại bỏ routes bị ẩn)
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var routes = await _db.Routes
            .Where(r => r.IsActive && !r.IsHidden)
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
        if (route == null || route.IsHidden) return NotFound(new { message = $"Route {id} không tồn tại." });

        var zones = await _db.Zones
            .Where(z => z.RouteId == id && z.IsActive && !z.IsHidden)
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

    // GET /api/routes/sync?lastSyncedAt=2026-03-01T00:00:00
    // Mobile gọi API này khi mở app để kiểm tra có dữ liệu mới không.
    [HttpGet("sync")]
    public async Task<IActionResult> Sync([FromQuery] DateTime? lastSyncedAt)
    {
        bool hasUpdates = true;

        if (lastSyncedAt.HasValue)
        {
            // Kiểm tra thời gian cập nhật mới nhất của tất cả các bảng
            var latestRouteUpdate = await _db.Routes.MaxAsync(r => (DateTime?)r.UpdatedAt) ?? DateTime.MinValue;
            var latestZoneUpdate = await _db.Zones.MaxAsync(z => (DateTime?)z.UpdatedAt) ?? DateTime.MinValue;
            var latestNarrationUpdate = await _db.Narrations.MaxAsync(n => (DateTime?)n.UpdatedAt) ?? DateTime.MinValue;

            var maxUpdatedAt = new[] { latestRouteUpdate, latestZoneUpdate, latestNarrationUpdate }.Max();

            // Nếu ngày cập nhật mới nhất trên server vẫn bé hơn hoặc bằng ngày dưới mobile -> không có gì mới
            if (maxUpdatedAt <= lastSyncedAt.Value)
            {
                hasUpdates = false;
            }
        }

        if (!hasUpdates)
        {
            return Ok(new { hasUpdates = false, message = "Dữ liệu trên app đã là bản mới nhất, không cần tải lại." });
        }

        // Nếu là lần đầu tiên tải app (lastSyncedAt = null) HOẶC có bản cập nhật mới, trả về toàn bộ để Mobile lưu
        var routes = await _db.Routes.Where(r => r.IsActive && !r.IsHidden).ToListAsync();
        var zones = await _db.Zones.Where(z => z.IsActive && !z.IsHidden).ToListAsync();
        var narrations = await _db.Narrations.ToListAsync();

        return Ok(new
        {
            hasUpdates = true,
            timestamp = DateTime.UtcNow, // Mobile sẽ lấy timestamp này lưu vào AppSetting["LastSyncedAt"]
            data = new
            {
                routes,
                zones,
                narrations
            }
        });
    }

}