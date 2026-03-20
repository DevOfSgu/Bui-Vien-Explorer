using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TravelSystem.Shared.Models;
using TravelSystem.Web.Data;

namespace TravelSystem.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FavoritesController : ControllerBase
{
    private readonly AppDbContext _db;

    public FavoritesController(AppDbContext db)
    {
        _db = db;
    }

    // GET /api/favorites/{guestId}
    // Lấy danh sách zones yêu thích của 1 guest (kèm thông tin zone)
    [HttpGet("{guestId}")]
    public async Task<IActionResult> GetByGuest(string guestId)
    {
        var rawFavorites = await _db.GuestFavorites
            .Where(f => f.GuestId == guestId)
            .Join(_db.Zones, f => f.ZoneId, z => z.Id, (f, z) => new
            {
                f.Id,
                f.GuestId,
                f.ZoneId,
                f.CreatedAt,
                Zone = new
                {
                    z.Name,
                    z.Description,
                    z.Latitude,
                    z.Longitude,
                    z.ShopId
                }
            })
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync();

        var favorites = rawFavorites
            .GroupBy(f => f.ZoneId)
            .Select(g => g.First())
            .ToList();

        return Ok(favorites);
    }

    // POST /api/favorites
    // Thêm zone vào danh sách yêu thích của guest
    [HttpPost]
    public async Task<IActionResult> Add([FromBody] AddFavoriteRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.GuestId) || request.ZoneId <= 0)
            return BadRequest(new { message = "GuestId và ZoneId là bắt buộc." });

        // Kiểm tra zone tồn tại
        var zoneExists = await _db.Zones.AnyAsync(z => z.Id == request.ZoneId);
        if (!zoneExists)
            return NotFound(new { message = $"Zone {request.ZoneId} không tồn tại." });

        // Kiểm tra đã like rồi chưa, đồng thời dọn dữ liệu trùng nếu có từ trước
        var existingFavorites = await _db.GuestFavorites
            .Where(f => f.GuestId == request.GuestId && f.ZoneId == request.ZoneId)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync();

        if (existingFavorites.Count > 1)
        {
            _db.GuestFavorites.RemoveRange(existingFavorites.Skip(1));
            await _db.SaveChangesAsync();
        }

        var alreadyExists = existingFavorites.Count > 0;

        if (alreadyExists)
            return Ok(new { message = "Đã có trong danh sách yêu thích.", alreadyFavorited = true });

        var favorite = new GuestFavorite
        {
            GuestId = request.GuestId,
            ZoneId = request.ZoneId,
            CreatedAt = DateTime.UtcNow
        };

        try
        {
            _db.GuestFavorites.Add(favorite);
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Trường hợp client gửi đúp gần như đồng thời
            return Ok(new { message = "Đã có trong danh sách yêu thích.", alreadyFavorited = true });
        }

        return Ok(new { message = "Đã thêm vào yêu thích.", favoriteId = favorite.Id, alreadyFavorited = false });
    }

    // DELETE /api/favorites/{guestId}/{zoneId}
    // Bỏ yêu thích
    [HttpDelete("{guestId}/{zoneId}")]
    public async Task<IActionResult> Remove(string guestId, int zoneId)
    {
        var favorite = await _db.GuestFavorites
            .FirstOrDefaultAsync(f => f.GuestId == guestId && f.ZoneId == zoneId);

        if (favorite == null)
            return NotFound(new { message = "Không tìm thấy mục yêu thích này." });

        _db.GuestFavorites.Remove(favorite);
        await _db.SaveChangesAsync();

        return Ok(new { message = "Đã bỏ yêu thích." });
    }

    // GET /api/favorites/top?limit=10
    // Top zones được nhiều guest yêu thích nhất (dùng cho Admin Dashboard)
    [HttpGet("top")]
    public async Task<IActionResult> GetTop([FromQuery] int limit = 10)
    {
        var topZones = await _db.GuestFavorites
            .GroupBy(f => f.ZoneId)
            .OrderByDescending(g => g.Count())
            .Take(limit)
            .Select(g => new
            {
                ZoneId = g.Key,
                FavoriteCount = g.Count()
            })
            .Join(_db.Zones, t => t.ZoneId, z => z.Id, (t, z) => new
            {
                t.ZoneId,
                t.FavoriteCount,
                ZoneName = z.Name,
                ZoneDescription = z.Description,
                Latitude = z.Latitude,
                Longitude = z.Longitude,
                ShopId = z.ShopId
            })
            .ToListAsync();

        return Ok(topZones);
    }
}

public record AddFavoriteRequest(string GuestId, int ZoneId);
