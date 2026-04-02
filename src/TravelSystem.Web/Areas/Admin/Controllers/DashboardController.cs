using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TravelSystem.Web.Data;

namespace TravelSystem.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(AuthenticationSchemes = "AdminAuth", Roles = "Admin")]
    public class DashboardController : Controller
    {
        private readonly AppDbContext _db;

        public DashboardController(AppDbContext db)
        {
            _db = db;
        }

        public async Task<IActionResult> Index()
        {
            var analytics = _db.Analytics.AsQueryable();

            ViewBag.TotalZones = await _db.Zones.CountAsync();
            ViewBag.TotalNarrations = await _db.Narrations.CountAsync();

            var utcToday = DateTime.UtcNow.Date;
            var utcTomorrow = utcToday.AddDays(1);

            ViewBag.ActiveUsers = await analytics
                .Where(a => a.CreatedAt >= utcToday && a.CreatedAt < utcTomorrow)
                .Select(a => a.SessionId)
                .Distinct()
                .CountAsync();

            // 1. Thời gian trung bình nghe 1 POI
            var avgDwellSeconds = await analytics
                .Where(a => a.ActionType.StartsWith("PlayNarration") && a.DwellTimeSeconds > 0)
                .Select(a => (double?)a.DwellTimeSeconds)
                .AverageAsync();
            ViewBag.AvgDwellTime = avgDwellSeconds.HasValue ? (int)Math.Round(avgDwellSeconds.Value) : 0;

            // 2. Top địa điểm được nghe nhiều nhất
            var topZoneCounts = await analytics
                .Where(a => a.ActionType.StartsWith("PlayNarration") && a.ZoneId.HasValue)
                .GroupBy(a => a.ZoneId!.Value)
                .Select(g => new { ZoneId = g.Key, Count = g.Count() })
                .OrderByDescending(g => g.Count)
                .Take(5)
                .ToListAsync();

            var topZonesIds = topZoneCounts.Select(x => x.ZoneId).ToList();

            var topZones = await _db.Zones.Where(z => topZonesIds.Contains(z.Id)).ToDictionaryAsync(z => z.Id, z => z.Name);
            
            var topList = topZoneCounts
                .Where(x => topZones.ContainsKey(x.ZoneId))
                .Select(x => new { Name = topZones[x.ZoneId], x.Count })
                .ToList();

            ViewBag.TopPoIs = topList;

            // 3. Top zones được nhiều guest yêu thích nhất (GuestFavorites)
            var topFavorites = await _db.GuestFavorites
                .GroupBy(f => f.ZoneId)
                .OrderByDescending(g => g.Count())
                .Take(5)
                .Select(g => new { ZoneId = g.Key, Count = g.Count() })
                .ToListAsync();

            var favoriteZoneIds = topFavorites.Select(t => t.ZoneId).ToList();
            var favoriteZones = await _db.Zones
                .Where(z => favoriteZoneIds.Contains(z.Id))
                .ToDictionaryAsync(z => z.Id, z => z.Name);

            ViewBag.TopFavorites = topFavorites
                .Select(t => new
                {
                    Name = favoriteZones.ContainsKey(t.ZoneId) ? favoriteZones[t.ZoneId] : $"Zone #{t.ZoneId}",
                    Count = t.Count
                })
                .ToList<dynamic>();

            ViewBag.TotalFavorites = await _db.GuestFavorites.CountAsync();

            // 4. Shop Owner Registration Stats
            var registrations = await _db.Users.Where(u => u.Role == 1).ToListAsync();
            var now = DateTime.UtcNow;

            // Monthly (Last 12)
            var monthlyReg = Enumerable.Range(0, 12).Select(i => {
                var month = now.AddMonths(-i);
                return new {
                    Label = month.ToString("MMM yyyy"),
                    Count = registrations.Count(r => r.CreatedAt.Month == month.Month && r.CreatedAt.Year == month.Year)
                };
            }).Reverse().ToList();

            ViewBag.MonthlyRegLabels = monthlyReg.Select(m => m.Label).ToList();
            ViewBag.MonthlyRegData = monthlyReg.Select(m => m.Count).ToList();
            
            // Daily (Last 30) - Use Dictionary for stable access in view
            var dailyReg = Enumerable.Range(0, 30).Select(i => {
                var date = now.AddDays(-i).Date;
                return new KeyValuePair<string, int>(date.ToString("dd/MM"), registrations.Count(r => r.CreatedAt.Date == date));
            }).ToList();
            ViewBag.DailyRegTable = dailyReg;

            // 5. App User Usage Stats (Unique Sessions)
            // Monthly (Last 12)
            var analyticsRows = await analytics
                .Select(a => new { a.SessionId, a.CreatedAt })
                .ToListAsync();

            var monthlyUsage = Enumerable.Range(0, 12).Select(i => {
                var month = now.AddMonths(-i);
                return new {
                    Label = month.ToString("MMM yyyy"),
                    Count = analyticsRows.Where(a => a.CreatedAt.Month == month.Month && a.CreatedAt.Year == month.Year)
                                         .Select(a => a.SessionId).Distinct().Count()
                };
            }).Reverse().ToList();

            ViewBag.MonthlyUsageLabels = monthlyUsage.Select(m => m.Label).ToList();
            ViewBag.MonthlyUsageData = monthlyUsage.Select(m => m.Count).ToList();

            // Daily (Last 30)
            var dailyUsage = Enumerable.Range(0, 30).Select(i => {
                var date = now.AddDays(-i).Date;
                return new KeyValuePair<string, int>(date.ToString("dd/MM"), 
                    analyticsRows.Where(a => a.CreatedAt.Date == date).Select(a => a.SessionId).Distinct().Count());
            }).ToList();
            ViewBag.DailyUsageTable = dailyUsage;

            // 6. Visits this week (unique sessions per day, UTC week Mon-Sun)
            var weekStart = utcToday.AddDays(-(((int)utcToday.DayOfWeek + 6) % 7)); // Monday
            var weekEnd = weekStart.AddDays(7);

            var weeklyRows = await analytics
                .Where(a => a.CreatedAt >= weekStart && a.CreatedAt < weekEnd)
                .Select(a => new { Day = a.CreatedAt.Date, a.SessionId })
                .ToListAsync();

            var weeklyLabels = Enumerable.Range(0, 7)
                .Select(i => weekStart.AddDays(i).ToString("ddd"))
                .ToList();
            var weeklyVisits = Enumerable.Range(0, 7)
                .Select(i =>
                {
                    var day = weekStart.AddDays(i);
                    return weeklyRows
                        .Where(x => x.Day == day)
                        .Select(x => x.SessionId)
                        .Distinct()
                        .Count();
                })
                .ToList();

            ViewBag.WeeklyVisitLabels = weeklyLabels;
            ViewBag.WeeklyVisitData = weeklyVisits;

            return View();
        }
    }
}
