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

    public ToursController(AppDbContext db, IHttpClientFactory httpClientFactory)
    {
        _db = db;
        _httpClient = httpClientFactory.CreateClient();
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var tours = await _db.Tours
            .AsNoTracking()
            .Include(t => t.TourZones)
            .OrderBy(t => t.Id)
            .Select(t => new
            {
                t.Id,
                t.Name,
                t.Description,
                t.ImageUrl,
                t.Duration,
                StopsCount = t.TourZones.Count(tz => tz.Zone != null && tz.Zone.IsActive && !tz.Zone.IsHidden)
            })
            .ToListAsync();

        return Ok(tours);
    }

    [HttpGet("{id:int}/stops")]
    public async Task<IActionResult> GetStops(int id)
    {
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
                Description = tz.Zone != null ? tz.Zone.Description : string.Empty,
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
        });

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

    private sealed record ShopHourView(int ShopId, int DayOfWeek, TimeSpan OpenTime, TimeSpan CloseTime);
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
