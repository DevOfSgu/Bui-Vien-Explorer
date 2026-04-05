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
    private static readonly HashSet<string> AllowedActionTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "AppInstall",
        "EnterZone",
        "PlayNarration",
        "LocationPing"
    };

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
            CreatedAt = NormalizeToUtc(request.CreatedAt),
            ZoneId = null

        });

        await _db.SaveChangesAsync();
        return Ok(new { created = true });
    }

    public sealed class AnonymousInstallRequest
    {
        public Guid SessionId { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    [HttpPost("event")]
    public async Task<IActionResult> TrackEvent([FromBody] AnalyticsEventRequest request)
    {
        if (request is null || request.SessionId == Guid.Empty)
        {
            return BadRequest(new { message = "SessionId is required." });
        }

        if (string.IsNullOrWhiteSpace(request.ActionType))
        {
            return BadRequest(new { message = "ActionType is required." });
        }

        var actionType = NormalizeActionType(request.ActionType);
        if (string.IsNullOrWhiteSpace(actionType) || !IsAllowedActionType(actionType))
        {
            return BadRequest(new { message = "Unsupported ActionType." });
        }

        if ((actionType.Equals("EnterZone", StringComparison.OrdinalIgnoreCase)
             || actionType.StartsWith("PlayNarration", StringComparison.OrdinalIgnoreCase))
            && (!request.ZoneId.HasValue || request.ZoneId.Value <= 0))
        {
            return BadRequest(new { message = "ZoneId is required for EnterZone and PlayNarration." });
        }

        _db.Analytics.Add(new Analytics
        {
            SessionId = request.SessionId,
            ActionType = actionType,
            ZoneId = request.ZoneId,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            DwellTimeSeconds = Math.Max(0, request.DwellTimeSeconds),
            CreatedAt = NormalizeToUtc(request.CreatedAt)
        });

        await _db.SaveChangesAsync();
        return Ok(new { created = true });
    }

    public sealed class AnalyticsEventRequest
    {
        public Guid SessionId { get; set; }
        public int? ZoneId { get; set; }
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
        public string ActionType { get; set; } = string.Empty;
        public int DwellTimeSeconds { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    private static bool IsAllowedActionType(string actionType)
    {
        if (AllowedActionTypes.Contains(actionType))
        {
            return true;
        }

        return actionType.StartsWith("PlayNarration|", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeActionType(string rawActionType)
    {
        var actionType = rawActionType.Trim();
        if (!actionType.StartsWith("PlayNarration|", StringComparison.OrdinalIgnoreCase))
        {
            return actionType;
        }

        var parts = actionType.Split('|', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[1]))
        {
            return "PlayNarration";
        }

        var lang = parts[1].ToLowerInvariant();
        return $"PlayNarration|{lang}";
    }

    private static DateTime NormalizeToUtc(DateTime dateTime)
    {
        if (dateTime == default)
        {
            return DateTime.UtcNow;
        }

        return dateTime.Kind switch
        {
            DateTimeKind.Utc => dateTime,
            DateTimeKind.Local => dateTime.ToUniversalTime(),
            _ => DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)
        };
    }
}
