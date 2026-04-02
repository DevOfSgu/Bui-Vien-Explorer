using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Json;
using TravelSystem.Web.Data;

namespace TravelSystem.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(AuthenticationSchemes = "AdminAuth", Roles = "Admin")]
    public class HeatmapController : Controller
    {
        private readonly AppDbContext _db;
        private readonly HttpClient _httpClient;

        // Requested corridor endpoints:
        // Start: 10.7379, 106.6790
        // End:   10.767900, 106.695036
        private const double StartLat = 10.7379d;
        private const double StartLng = 106.6790d;
        private const double EndLat = 10.767900d;
        private const double EndLng = 106.695036d;
        private const double CorridorMeters = 95d;

        public HeatmapController(AppDbContext db, IHttpClientFactory httpClientFactory)
        {
            _db = db;
            _httpClient = httpClientFactory.CreateClient();
        }

        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            var routePolyline = await GetBuiVienRouteAsync(cancellationToken);
            if (routePolyline.Count < 2)
            {
                routePolyline =
                [
                    new GeoPointVm { Latitude = StartLat, Longitude = StartLng },
                    new GeoPointVm { Latitude = EndLat, Longitude = EndLng }
                ];
            }

            var rows = await _db.Analytics
                .AsNoTracking()
                .Where(a =>
                    a.ActionType == "EnterZone" ||
                    a.ActionType == "LocationPing" ||
                    a.ActionType.StartsWith("PlayNarration"))
                .Select(a => new { a.ZoneId, a.Latitude, a.Longitude, a.ActionType, a.SessionId })
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

            var projectedHeatBuckets = new Dictionary<string, (double Latitude, double Longitude, double Score)>();
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

                var nearest = FindNearestPointOnRoute(lat, lng, routePolyline);
                if (nearest.DistanceMeters > CorridorMeters)
                {
                    continue;
                }

                matchedRows += 1;
                var score = GetActionWeight(row.ActionType);
                var key = $"{Math.Round(nearest.ProjectedLatitude, 5)}|{Math.Round(nearest.ProjectedLongitude, 5)}";

                if (projectedHeatBuckets.TryGetValue(key, out var existing))
                {
                    projectedHeatBuckets[key] = (existing.Latitude, existing.Longitude, existing.Score + score);
                }
                else
                {
                    projectedHeatBuckets[key] = (nearest.ProjectedLatitude, nearest.ProjectedLongitude, score);
                }
            }

            var maxBucket = projectedHeatBuckets.Count == 0 ? 1d : projectedHeatBuckets.Values.Max(v => v.Score);
            var heatPoints = new List<HeatPointVm>();

            // baseline low intensity over the route to create continuous corridor-like strip
            for (var i = 0; i < routePolyline.Count; i += 4)
            {
                heatPoints.Add(new HeatPointVm
                {
                    Latitude = routePolyline[i].Latitude,
                    Longitude = routePolyline[i].Longitude,
                    Intensity = 0.07d
                });
            }

            foreach (var bucket in projectedHeatBuckets.Values)
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
                StartLatitude = StartLat,
                StartLongitude = StartLng,
                EndLatitude = EndLat,
                EndLongitude = EndLng,
                CorridorMeters = (int)CorridorMeters,
                TotalAnalyticsRows = rows.Count,
                MatchedRows = matchedRows,
                RoutePolyline = routePolyline,
                HeatPoints = heatPoints
            };

            return View(vm);
        }

        private async Task<List<GeoPointVm>> GetBuiVienRouteAsync(CancellationToken cancellationToken)
        {
            var coords = $"{StartLng},{StartLat};{EndLng},{EndLat}";
            var url = $"http://router.project-osrm.org/route/v1/walking/{coords}?overview=full&geometries=geojson";

            try
            {
                var osrm = await _httpClient.GetFromJsonAsync<OsrmResponse>(url, cancellationToken);
                var points = osrm?.Routes?.FirstOrDefault()?.Geometry?.Coordinates;
                if (points == null || points.Count == 0)
                {
                    return [];
                }

                return points.Select(c => new GeoPointVm
                {
                    Longitude = c[0],
                    Latitude = c[1]
                }).ToList();
            }
            catch
            {
                return [];
            }
        }

        private static double GetActionWeight(string? actionType)
        {
            if (string.IsNullOrWhiteSpace(actionType))
            {
                return 1d;
            }

            if (actionType.StartsWith("PlayNarration", StringComparison.OrdinalIgnoreCase))
            {
                return 3.2d;
            }

            if (actionType.Equals("EnterZone", StringComparison.OrdinalIgnoreCase))
            {
                return 2.2d;
            }

            return 1d;
        }

        private static NearestProjection FindNearestPointOnRoute(double lat, double lng, List<GeoPointVm> polyline)
        {
            var nearest = new NearestProjection
            {
                DistanceMeters = double.MaxValue,
                ProjectedLatitude = lat,
                ProjectedLongitude = lng
            };

            var point = new GeoPointVm { Latitude = lat, Longitude = lng };

            for (var i = 0; i < polyline.Count - 1; i++)
            {
                var segStart = polyline[i];
                var segEnd = polyline[i + 1];
                var projection = ProjectToSegment(point, segStart, segEnd);
                if (projection.DistanceMeters < nearest.DistanceMeters)
                {
                    nearest = projection;
                }
            }

            return nearest;
        }

        private static NearestProjection ProjectToSegment(GeoPointVm point, GeoPointVm start, GeoPointVm end)
        {
            var latScale = 111320d;
            var avgLatRad = DegreesToRadians((start.Latitude + end.Latitude) / 2d);
            var lonScale = Math.Cos(avgLatRad) * latScale;

            var px = point.Longitude * lonScale;
            var py = point.Latitude * latScale;
            var x1 = start.Longitude * lonScale;
            var y1 = start.Latitude * latScale;
            var x2 = end.Longitude * lonScale;
            var y2 = end.Latitude * latScale;

            var dx = x2 - x1;
            var dy = y2 - y1;

            if (Math.Abs(dx) < double.Epsilon && Math.Abs(dy) < double.Epsilon)
            {
                return new NearestProjection
                {
                    ProjectedLatitude = start.Latitude,
                    ProjectedLongitude = start.Longitude,
                    DistanceMeters = Math.Sqrt(Math.Pow(px - x1, 2) + Math.Pow(py - y1, 2))
                };
            }

            var t = ((px - x1) * dx + (py - y1) * dy) / ((dx * dx) + (dy * dy));
            t = Math.Max(0, Math.Min(1, t));

            var projX = x1 + (t * dx);
            var projY = y1 + (t * dy);

            return new NearestProjection
            {
                ProjectedLatitude = projY / latScale,
                ProjectedLongitude = projX / lonScale,
                DistanceMeters = Math.Sqrt(Math.Pow(px - projX, 2) + Math.Pow(py - projY, 2))
            };
        }

        private static double DegreesToRadians(double degree) => degree * (Math.PI / 180d);

        public sealed class HeatmapViewModel
        {
            public double StartLatitude { get; set; }
            public double StartLongitude { get; set; }
            public double EndLatitude { get; set; }
            public double EndLongitude { get; set; }
            public int CorridorMeters { get; set; }
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

        private sealed class NearestProjection
        {
            public double ProjectedLatitude { get; set; }
            public double ProjectedLongitude { get; set; }
            public double DistanceMeters { get; set; }
        }

        private sealed class OsrmResponse
        {
            public List<OsrmRoute> Routes { get; set; } = [];
        }

        private sealed class OsrmRoute
        {
            public OsrmGeometry? Geometry { get; set; }
        }

        private sealed class OsrmGeometry
        {
            public List<List<double>> Coordinates { get; set; } = [];
        }
    }
}
