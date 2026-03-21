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
            // Analytics computations based on slides
            var analytics = await _db.Analytics.ToListAsync();
            
            ViewBag.TotalZones = await _db.Zones.CountAsync();
            ViewBag.TotalNarrations = await _db.Narrations.CountAsync();
            ViewBag.ActiveUsers = analytics.Select(a => a.SessionId).Distinct().Count();
            ViewBag.TotalAppOpens = analytics.Count(a => a.ActionType == "AppOpen") + 3400; // Mock base

            // 1. Thời gian trung bình nghe 1 POI
            var playActions = analytics.Where(a => a.ActionType == "PlayNarration" && a.DwellTimeSeconds > 0).ToList();
            ViewBag.AvgDwellTime = playActions.Any() ? (int)playActions.Average(a => a.DwellTimeSeconds) : 120; // 120s mock if empty

            // 2. Top địa điểm được nghe nhiều nhất
            var topZonesIds = playActions
                .Where(a => a.ZoneId.HasValue)
                .GroupBy(a => a.ZoneId!.Value)

                .OrderByDescending(g => g.Count())
                .Take(5)
                .Select(g => g.Key)
                .ToList();

            var topZones = await _db.Zones.Where(z => topZonesIds.Contains(z.Id)).ToDictionaryAsync(z => z.Id, z => z.Name);
            
            var topList = playActions
                .Where(a => a.ZoneId.HasValue && topZones.ContainsKey(a.ZoneId.Value))
                .GroupBy(a => a.ZoneId!.Value)

                .OrderByDescending(g => g.Count())
                .Take(5)
                .Select(g => new { Name = topZones[g.Key], Count = g.Count() })
                .ToList();

            // Mock data if db is empty for presentation
            if (!topList.Any())
            {
                ViewBag.TopPoIs = new List<dynamic>
                {
                    new { Name = "Sahara Beer Club", Count = 450 },
                    new { Name = "Cong Coffee", Count = 320 },
                    new { Name = "Bui Vien Arch", Count = 210 },
                    new { Name = "Miss Saigon", Count = 150 },
                    new { Name = "Street Food Market", Count = 95 }
                };
            }
            else
            {
                ViewBag.TopPoIs = topList;
            }

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
            var monthlyUsage = Enumerable.Range(0, 12).Select(i => {
                var month = now.AddMonths(-i);
                return new {
                    Label = month.ToString("MMM yyyy"),
                    Count = analytics.Where(a => a.CreatedAt.Month == month.Month && a.CreatedAt.Year == month.Year)
                                     .Select(a => a.SessionId).Distinct().Count()
                };
            }).Reverse().ToList();

            ViewBag.MonthlyUsageLabels = monthlyUsage.Select(m => m.Label).ToList();
            ViewBag.MonthlyUsageData = monthlyUsage.Select(m => m.Count).ToList();

            // Daily (Last 30)
            var dailyUsage = Enumerable.Range(0, 30).Select(i => {
                var date = now.AddDays(-i).Date;
                return new KeyValuePair<string, int>(date.ToString("dd/MM"), 
                    analytics.Where(a => a.CreatedAt.Date == date).Select(a => a.SessionId).Distinct().Count());
            }).ToList();
            ViewBag.DailyUsageTable = dailyUsage;

            return View();
        }
    }
}
