using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TravelSystem.Shared.Models;
using TravelSystem.Web.Data;

namespace TravelSystem.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AnalyticsController : ControllerBase
{
    private readonly AppDbContext _db;

    public AnalyticsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpPost("install")]
    public async Task<IActionResult> RegisterInstall([FromBody] AnonymousInstallRequest request)
    {
        if (request is null || request.SessionId == Guid.Empty)
        {
            return BadRequest(new { message = "SessionId is required." });
        }

        var exists = await _db.Analytics
            .AnyAsync(a => a.SessionId == request.SessionId && a.ActionType == "AppInstall");

        if (exists)
        {
            return Ok(new { created = false, message = "Install already recorded." });
        }

        _db.Analytics.Add(new Analytics
        {
            SessionId = request.SessionId,
            ActionType = "AppInstall",
            Latitude = 0,
            Longitude = 0,
            DwellTimeSeconds = 0,
            CreatedAt = request.CreatedAt == default ? DateTime.UtcNow : request.CreatedAt,
            ZoneId = null,
            RouteId = null
        });

        await _db.SaveChangesAsync();
        return Ok(new { created = true });
    }

    public sealed class AnonymousInstallRequest
    {
        public Guid SessionId { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
