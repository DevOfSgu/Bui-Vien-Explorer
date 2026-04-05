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

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] int? zoneId, [FromQuery] string language = "vi")
    {
        try
        {
            var normalizedLanguage = NormalizeLanguage(language);

            if (zoneId == null || zoneId == 0)
            {
                var allApprovedNarrations = await _db.Narrations
                    .Where(n => n.ApprovalStatus == "Approved")
                    .OrderByDescending(n => n.UpdatedAt)
                    .ThenByDescending(n => n.Id)
                    .ToListAsync();

                var latestNarrations = allApprovedNarrations
                    .GroupBy(n => new { n.ZoneId, Language = NormalizeLanguage(n.Language) })
                    .Select(g => g.First())
                    .ToList();

                return Ok(latestNarrations);
            }

            var candidates = await _db.Narrations
                .Where(n => n.ZoneId == zoneId.Value
                    && n.ApprovalStatus == "Approved")
                .OrderByDescending(n => n.UpdatedAt)
                .ThenByDescending(n => n.Id)
                .ToListAsync();

            var narration = candidates
                .FirstOrDefault(n => NormalizeLanguage(n.Language) == normalizedLanguage);

            if (narration == null)
                return NotFound(new { message = $"Không tìm thấy narration cho zone {zoneId} / ngôn ngữ '{language}'." });

            return Ok(narration);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Lỗi truy vấn Narrations", detail = ex.Message, stack = ex.StackTrace });
        }
    }

    // GET /api/narrations/zone/{zoneId}
    // Lấy tất cả narrations (mọi ngôn ngữ) của 1 zone
    [HttpGet("zone/{zoneId}")]
    public async Task<IActionResult> GetAllByZone(int zoneId)
    {
        var narrations = await _db.Narrations
            .Where(n => n.ZoneId == zoneId && n.ApprovalStatus == "Approved")
            .OrderByDescending(n => n.UpdatedAt)
            .ThenByDescending(n => n.Id)
            .ToListAsync();

        var latestNarrations = narrations
            .GroupBy(n => NormalizeLanguage(n.Language))
            .Select(g => g.First())
            .ToList();

        return Ok(latestNarrations);
    }

    private static string NormalizeLanguage(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return "vi";
        }

        var normalized = language.Trim().ToLowerInvariant();
        var dashIndex = normalized.IndexOf('-');
        if (dashIndex > 0)
        {
            normalized = normalized[..dashIndex];
        }

        return normalized.Length > 5 ? normalized[..5] : normalized;
    }
}
