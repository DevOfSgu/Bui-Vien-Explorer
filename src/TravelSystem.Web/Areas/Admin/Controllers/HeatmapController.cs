using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TravelSystem.Web.Data;

namespace TravelSystem.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(AuthenticationSchemes = "AdminAuth", Roles = "Admin")]
    public class HeatmapController : Controller
    {
        private readonly AppDbContext _db;

        public HeatmapController(AppDbContext db)
        {
            _db = db;
        }

        public async Task<IActionResult> Index(CancellationToken cancellationToken = default)
        {
            var routePolyline = new List<GeoPointVm>();

            var analyticsQuery = _db.Analytics
                .AsNoTracking()
                .Where(a => a.ActionType == "EnterZone");

            var rows = await analyticsQuery
                .Select(a => new { a.ZoneId, a.Latitude, a.Longitude, a.SessionId })
                .ToListAsync(cancellationToken);

            var zoneIds = rows
                .Where(r => r.ZoneId.HasValue)
                .Select(r => r.ZoneId!.Value)
                .Distinct()
                .ToList();

            var zoneCoords = await _db.Zones
                .AsNoTracking()
                .Where(z => zoneIds.Contains(z.Id))
                .Select(z => new { z.Id, z.Latitude, z.Longitude })
                .ToDictionaryAsync(z => z.Id, z => new GeoPointVm
                {
                    Latitude = (double)z.Latitude,
                    Longitude = (double)z.Longitude
                }, cancellationToken);

            var heatBuckets = new Dictionary<string, (double Latitude, double Longitude, double Score)>();
            var matchedRows = 0;

            foreach (var row in rows)
            {
                var lat = (double)row.Latitude;
                var lng = (double)row.Longitude;

                if (lat == 0 && lng == 0 && row.ZoneId.HasValue && zoneCoords.TryGetValue(row.ZoneId.Value, out var zonePoint))
                {
                    lat = zonePoint.Latitude;
                    lng = zonePoint.Longitude;
                }

                if (lat == 0 && lng == 0)
                {
                    continue;
                }

                matchedRows += 1;
                const double score = 1d;
                var bucketLat = Math.Round(lat, 5);
                var bucketLng = Math.Round(lng, 5);
                var key = $"{bucketLat}|{bucketLng}";

                if (heatBuckets.TryGetValue(key, out var existing))
                {
                    heatBuckets[key] = (existing.Latitude, existing.Longitude, existing.Score + score);
                }
                else
                {
                    heatBuckets[key] = (bucketLat, bucketLng, score);
                }
            }

            var maxBucket = heatBuckets.Count == 0 ? 1d : heatBuckets.Values.Max(v => v.Score);
            var heatPoints = new List<HeatPointVm>();

            foreach (var bucket in heatBuckets.Values)
            {
                var normalized = maxBucket <= 0 ? 0 : bucket.Score / maxBucket;
                heatPoints.Add(new HeatPointVm
                {
                    Latitude = bucket.Latitude,
                    Longitude = bucket.Longitude,
                    Intensity = Math.Min(1d, 0.2d + (normalized * 0.8d))
                });
            }

            var vm = new HeatmapViewModel
            {
                StartLatitude = heatPoints.FirstOrDefault()?.Latitude ?? 0,
                StartLongitude = heatPoints.FirstOrDefault()?.Longitude ?? 0,
                EndLatitude = heatPoints.LastOrDefault()?.Latitude ?? 0,
                EndLongitude = heatPoints.LastOrDefault()?.Longitude ?? 0,
                TotalAnalyticsRows = rows.Count,
                MatchedRows = matchedRows,
                RoutePolyline = routePolyline,
                HeatPoints = heatPoints
            };

            return View(vm);
        }

        public sealed class HeatmapViewModel
        {
            public double StartLatitude { get; set; }
            public double StartLongitude { get; set; }
            public double EndLatitude { get; set; }
            public double EndLongitude { get; set; }
            public int TotalAnalyticsRows { get; set; }
            public int MatchedRows { get; set; }
            public List<GeoPointVm> RoutePolyline { get; set; } = [];
            public List<HeatPointVm> HeatPoints { get; set; } = [];
        }

        public sealed class GeoPointVm
        {
            public double Latitude { get; set; }
            public double Longitude { get; set; }
        }

        public sealed class HeatPointVm
        {
            public double Latitude { get; set; }
            public double Longitude { get; set; }
            public double Intensity { get; set; }
        }

    }
}
