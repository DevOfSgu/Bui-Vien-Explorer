using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TravelSystem.Web.Data;

namespace TravelSystem.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NarrationsController : ControllerBase
{
    private readonly AppDbContext _db;

    public NarrationsController(AppDbContext db)
    {
        _db = db;
    }

    // GET /api/narrations?zoneId=1&language=vi
    // Lấy kịch bản TTS: nếu không có zoneId thì lấy tất cả (để sync)
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] int? zoneId, [FromQuery] string language = "vi")
    {
        if (zoneId == null || zoneId == 0)
        {
            var allNarrations = await _db.Narrations.ToListAsync();
            return Ok(allNarrations);
        }

        var narration = await _db.Narrations
            .FirstOrDefaultAsync(n => n.ZoneId == zoneId.Value && n.Language == language);

        if (narration == null)
            return NotFound(new { message = $"Không tìm thấy narration cho zone {zoneId} / ngôn ngữ '{language}'." });

        return Ok(narration);
    }

    // GET /api/narrations/zone/{zoneId}
    // Lấy tất cả narrations (mọi ngôn ngữ) của 1 zone
    [HttpGet("zone/{zoneId}")]
    public async Task<IActionResult> GetAllByZone(int zoneId)
    {
        var narrations = await _db.Narrations
            .Where(n => n.ZoneId == zoneId)
            .ToListAsync();
        return Ok(narrations);
    }
}
