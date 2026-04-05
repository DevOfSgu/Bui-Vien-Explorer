using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using TravelSystem.Web.Data;
using TravelSystem.Web.Models;

namespace TravelSystem.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ToursController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly HttpClient _httpClient;
    private readonly ILogger<ToursController> _logger;

    public ToursController(AppDbContext db, IHttpClientFactory httpClientFactory, ILogger<ToursController> logger)
    {
        _db = db;
        _httpClient = httpClientFactory.CreateClient();
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? lang = null)
    {
        var language = NormalizeLanguage(lang);

        var tours = await _db.Tours
            .AsNoTracking()
            .OrderBy(t => t.Id)
            .Select(t => new
            {
                t.Id,
                LegacyName = t.Name,
                LegacyDescription = t.Description,
                RequestedTranslation = t.Translations
                    .Where(tt => tt.Language == language)
                    .Select(tt => new { tt.Name, tt.Description })
                    .FirstOrDefault(),
                VietnameseTranslation = t.Translations
                    .Where(tt => tt.Language == "vi")
                    .Select(tt => new { tt.Name, tt.Description })
                    .FirstOrDefault(),
                AnyTranslation = t.Translations
                    .OrderBy(tt => tt.Id)
                    .Select(tt => new { tt.Name, tt.Description })
                    .FirstOrDefault(),
                t.ImageUrl,
                StoredDuration = t.Duration,
                Stops = t.TourZones
                    .Where(tz => tz.Zone != null && tz.Zone.IsActive && !tz.Zone.IsHidden)
                    .OrderBy(tz => tz.OrderIndex)
                    .Select(tz => new TourStopCoordinate
                    {
                        Latitude = tz.Zone != null ? tz.Zone.Latitude : 0,
                        Longitude = tz.Zone != null ? tz.Zone.Longitude : 0
                    })
                    .ToList()
            })
            .ToListAsync();

        var response = tours.Select(t => new
        {
            t.Id,
            Name = t.RequestedTranslation?.Name
                ?? t.VietnameseTranslation?.Name
                ?? t.AnyTranslation?.Name
                ?? t.LegacyName,
            Description = t.RequestedTranslation?.Description
                ?? t.VietnameseTranslation?.Description
                ?? t.AnyTranslation?.Description
                ?? t.LegacyDescription,
            t.ImageUrl,
            Duration = EstimateTourDurationMinutes(t.Stops, t.StoredDuration),
            StopsCount = t.Stops.Count
        });

        return Ok(response);
    }

    [HttpGet("{id:int}/stops")]
    public async Task<IActionResult> GetStops(int id, [FromQuery] string? lang = null)
    {
        var language = NormalizeLanguage(lang);

        var tourExists = await _db.Tours.AsNoTracking().AnyAsync(t => t.Id == id);
        if (!tourExists)
        {
            return NotFound(new { message = $"Tour {id} không tồn tại." });
        }

        var stops = await _db.TourZones
            .AsNoTracking()
            .Where(tz => tz.TourId == id && tz.Zone != null && tz.Zone.IsActive && !tz.Zone.IsHidden)
            .OrderBy(tz => tz.OrderIndex)
            .Select(tz => new
            {
                tz.ZoneId,
                tz.OrderIndex,
                Name = tz.Zone != null ? tz.Zone.Name : string.Empty,
                Description = tz.Zone == null
                    ? string.Empty
                    : tz.Zone.Translations
                        .Where(t => t.Language == language)
                        .Select(t => t.Description)
                        .FirstOrDefault()
                        ?? tz.Zone.Translations
                            .Where(t => t.Language == "vi")
                            .Select(t => t.Description)
                            .FirstOrDefault()
                        ?? tz.Zone.Translations
                            .OrderBy(t => t.Id)
                            .Select(t => t.Description)
                            .FirstOrDefault()
                        ?? tz.Zone.Description,
                ImageUrl = tz.Zone != null ? tz.Zone.ImageUrl : null,
                Latitude = tz.Zone != null ? tz.Zone.Latitude : 0,
                Longitude = tz.Zone != null ? tz.Zone.Longitude : 0,
                Radius = tz.Zone != null ? tz.Zone.Radius : 0,
                ShopId = tz.Zone != null ? tz.Zone.ShopId : null,
                IsMain = tz.Zone != null && tz.Zone.IsMain
            })
            .ToListAsync();

        var shopIds = stops
            .Where(s => s.ShopId.HasValue)
            .Select(s => s.ShopId!.Value)
            .Distinct()
            .ToList();

        var shopAddressMap = new Dictionary<int, string?>();
        var shopHoursMap = new Dictionary<int, string>();

        if (shopIds.Count > 0)
        {
            shopAddressMap = await _db.Shops
                .AsNoTracking()
                .Where(s => shopIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, s => s.Address);

            var rawHours = await _db.ShopHours
                .AsNoTracking()
                .Where(h => shopIds.Contains(h.ShopId))
                .Select(h => new ShopHourView(h.ShopId, h.DayOfWeek, h.OpenTime, h.CloseTime))
                .ToListAsync();

            shopHoursMap = rawHours
                .GroupBy(h => h.ShopId)
                .ToDictionary(g => g.Key, g => FormatHours(g));
        }

        var response = stops.Select(s => new
        {
            s.ZoneId,
            s.OrderIndex,
            s.Name,
            s.Description,
            s.ImageUrl,
            s.Latitude,
            s.Longitude,
            s.Radius,
            s.IsMain,
            Address = s.ShopId.HasValue && shopAddressMap.TryGetValue(s.ShopId.Value, out var address) ? address : null,
            Hours = s.ShopId.HasValue && shopHoursMap.TryGetValue(s.ShopId.Value, out var hours) ? hours : null
        }).ToList();

        _logger.LogInformation("[TOUR_STOPS_DIAG] TourId={TourId}, Lang={Lang}, Stops={Count}", id, language, response.Count);
        foreach (var stop in response.Take(10))
        {
            var preview = string.IsNullOrWhiteSpace(stop.Description)
                ? "(empty)"
                : (stop.Description.Length > 120 ? stop.Description[..120] + "..." : stop.Description);

            _logger.LogInformation("[TOUR_STOPS_DIAG] ZoneId={ZoneId}, Idx={OrderIndex}, Name={Name}, Desc={Desc}",
                stop.ZoneId, stop.OrderIndex, stop.Name, preview);
        }

        return Ok(response);
    }

    private static string FormatHours(IEnumerable<ShopHourView> hours)
    {
        var hourItems = hours
            .OrderBy(h => h.DayOfWeek)
            .ThenBy(h => h.OpenTime)
            .ToList();

        if (hourItems.Count == 0)
        {
            return "--";
        }

        var first = hourItems[0];
        var sameTimeForAllDays = hourItems.All(h => h.OpenTime == first.OpenTime
            && h.CloseTime == first.CloseTime);

        if (sameTimeForAllDays)
        {
            return $"{FormatTime(first.OpenTime)} - {FormatTime(first.CloseTime)}";
        }

        // Fallback: show today's schedule if present, otherwise earliest schedule.
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

    private static int EstimateTourDurationMinutes(IReadOnlyList<TourStopCoordinate> stops, int fallbackDurationMinutes)
    {
        if (stops.Count == 0)
        {
            return fallbackDurationMinutes > 0 ? fallbackDurationMinutes : 10;
        }

        const double averageWalkingKmPerHour = 4.2d;
        const double averageDwellMinutesPerStop = 7d;
        const double transitionBufferMinutes = 4d;

        double totalDistanceKm = 0d;
        for (var i = 1; i < stops.Count; i++)
        {
            totalDistanceKm += CalculateDistanceKm(stops[i - 1], stops[i]);
        }

        var walkingMinutes = (totalDistanceKm / averageWalkingKmPerHour) * 60d;
        var dwellMinutes = stops.Count * averageDwellMinutesPerStop;
        var estimatedMinutes = walkingMinutes + dwellMinutes + transitionBufferMinutes;

        if (estimatedMinutes <= 0)
        {
            estimatedMinutes = fallbackDurationMinutes > 0 ? fallbackDurationMinutes : 10;
        }

        // Round to nearest 5 minutes for a stable UI value.
        var roundedMinutes = (int)Math.Ceiling(estimatedMinutes / 5d) * 5;
        return Math.Max(10, roundedMinutes);
    }

    private static double CalculateDistanceKm(TourStopCoordinate from, TourStopCoordinate to)
    {
        const double earthRadiusKm = 6371d;

        var lat1 = DegreesToRadians((double)from.Latitude);
        var lon1 = DegreesToRadians((double)from.Longitude);
        var lat2 = DegreesToRadians((double)to.Latitude);
        var lon2 = DegreesToRadians((double)to.Longitude);

        var dLat = lat2 - lat1;
        var dLon = lon2 - lon1;

        var a = Math.Pow(Math.Sin(dLat / 2), 2)
            + Math.Cos(lat1) * Math.Cos(lat2) * Math.Pow(Math.Sin(dLon / 2), 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return earthRadiusKm * c;
    }

    private static double DegreesToRadians(double degrees)
    {
        return degrees * (Math.PI / 180d);
    }

    private static string NormalizeLanguage(string? lang)
    {
        if (string.IsNullOrWhiteSpace(lang))
        {
            return "vi";
        }

        var normalized = lang.Trim().ToLowerInvariant();
        var dashIndex = normalized.IndexOf('-');
        if (dashIndex > 0)
        {
            normalized = normalized[..dashIndex];
        }

        return normalized.Length > 5 ? normalized[..5] : normalized;
    }

    private sealed record ShopHourView(int ShopId, int DayOfWeek, TimeSpan OpenTime, TimeSpan CloseTime);
    private sealed class TourStopCoordinate
    {
        public decimal Latitude { get; init; }
        public decimal Longitude { get; init; }
    }

    [HttpGet("{id:int}/route")]
    public async Task<IActionResult> GetRoute(int id)
    {
        var stops = await _db.TourZones
            .AsNoTracking()
            .Where(tz => tz.TourId == id && tz.Zone != null && tz.Zone.IsActive && !tz.Zone.IsHidden)
            .OrderBy(tz => tz.OrderIndex)
            .Select(tz => new
            {
                Latitude = tz.Zone != null ? tz.Zone.Latitude : 0,
                Longitude = tz.Zone != null ? tz.Zone.Longitude : 0
            })
            .ToListAsync();

        if (stops.Count < 2)
            return Ok(new List<double[]>());

        var coords = string.Join(";", stops.Select(s => $"{s.Longitude},{s.Latitude}"));
        var url = $"http://router.project-osrm.org/route/v1/walking/{coords}?overview=full&geometries=geojson";

        try
        {
            var osrm = await _httpClient.GetFromJsonAsync<OsrmResponse>(url);
            var coordinates = osrm?.Routes?.FirstOrDefault()?.Geometry?.Coordinates;

            if (coordinates == null || coordinates.Count == 0)
                return Ok(stops.Select(s => new double[] { (double)s.Longitude, (double)s.Latitude }).ToList());

            return Ok(coordinates);
        }
        catch
        {
            return Ok(stops.Select(s => new double[] { (double)s.Longitude, (double)s.Latitude }).ToList());
        }
    }
}
