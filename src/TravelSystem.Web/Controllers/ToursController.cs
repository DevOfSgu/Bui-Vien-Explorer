using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
                Longitude = tz.Zone != null ? tz.Zone.Longitude : 0
            })
            .ToListAsync();

        return Ok(stops);
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
