using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
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

    // GET /api/zones
    // Lấy danh sách tất cả zones (loại bỏ zones bị ẩn)
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var zones = await _db.Zones
            .Where(z => z.IsActive && !z.IsHidden)
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

    // GET /api/zones/{id}/detail
    // Lấy chi tiết zone kèm địa chỉ + giờ mở cửa của shop liên kết (nếu có)
    [HttpGet("{id:int}/detail")]
    public async Task<IActionResult> GetDetail(int id)
    {
        var zone = await _db.Zones
            .AsNoTracking()
            .FirstOrDefaultAsync(z => z.Id == id && z.IsActive && !z.IsHidden);

        if (zone == null)
        {
            return NotFound(new { message = $"Zone {id} không tồn tại." });
        }

        string? address = null;
        string? hours = null;

        if (zone.ShopId.HasValue)
        {
            var shopId = zone.ShopId.Value;

            address = await _db.Shops
                .AsNoTracking()
                .Where(s => s.Id == shopId)
                .Select(s => s.Address)
                .FirstOrDefaultAsync();

            var shopHours = await _db.ShopHours
                .AsNoTracking()
                .Where(h => h.ShopId == shopId)
                .Select(h => new ShopHourView(h.DayOfWeek, h.OpenTime, h.CloseTime))
                .ToListAsync();

            hours = FormatHours(shopHours);
        }

        return Ok(new
        {
            zone.Id,
            zone.Name,
            zone.Description,
            zone.ImageUrl,
            zone.Latitude,
            zone.Longitude,
            zone.Radius,
            Address = address,
            Hours = hours
        });
    }

    private static string? FormatHours(IEnumerable<ShopHourView> hours)
    {
        var hourItems = hours
            .OrderBy(h => h.DayOfWeek)
            .ThenBy(h => h.OpenTime)
            .ToList();

        if (hourItems.Count == 0)
        {
            return null;
        }

        var first = hourItems[0];
        var sameTimeForAllDays = hourItems.All(h => h.OpenTime == first.OpenTime && h.CloseTime == first.CloseTime);
        if (sameTimeForAllDays)
        {
            return $"{FormatTime(first.OpenTime)} - {FormatTime(first.CloseTime)}";
        }

        var today = DateTime.Now.DayOfWeek switch
        {
            DayOfWeek.Sunday => 7,
            _ => (int)DateTime.Now.DayOfWeek
        };

        var todaySlot = hourItems.FirstOrDefault(h => h.DayOfWeek == today) ?? first;
        return $"{FormatTime(todaySlot.OpenTime)} - {FormatTime(todaySlot.CloseTime)}";
    }

    private static string FormatTime(TimeSpan time)
    {
        var dateTime = DateTime.Today.Add(time);
        return dateTime.ToString("h:mm tt", CultureInfo.InvariantCulture);
    }

    private sealed record ShopHourView(int DayOfWeek, TimeSpan OpenTime, TimeSpan CloseTime);
}
