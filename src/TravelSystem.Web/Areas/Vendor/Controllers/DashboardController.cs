using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TravelSystem.Web.Data;

namespace TravelSystem.Web.Areas.Vendor.Controllers
{
    [Area("Vendor")]
    [Authorize(AuthenticationSchemes = "VendorAuth", Roles = "Vendor")]
    public class DashboardController : Controller
    {
        private readonly AppDbContext _db;

        public DashboardController(AppDbContext db)
        {
            _db = db;
        }

        public async Task<IActionResult> Index()
        {
            var shopId = await ResolveCurrentVendorShopIdAsync();
            if (shopId == null)
            {
                await HttpContext.SignOutAsync("VendorAuth");
                return RedirectToAction("Login", "Auth", new { area = "Vendor" });
            }

            var shopZones = await _db.Zones
                .AsNoTracking()
                .Where(z => z.ShopId == shopId.Value)
                .ToListAsync();
            var shopZoneIds = shopZones.Select(z => z.Id).ToList();

            var shop = await _db.Shops
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == shopId.Value);
            ViewBag.ShopName = shop?.Name ?? "Your Shop";

            // Compute open/closed based on shop hours in DB (ShopHours table)
            // ShopHours has DayOfWeek (1=Mon, 7=Sun), OpenTime/CloseTime as TIME, IsOpen flag.
            var now = DateTime.Now;
            var today = now.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)now.DayOfWeek;
            var currentTime = now.TimeOfDay;

            var todaysHours = await _db.ShopHours
                .AsNoTracking()
                .Where(h => h.ShopId == shopId.Value && h.DayOfWeek == today)
                .ToListAsync();

            bool shopOpen = false;
            foreach (var h in todaysHours)
            {
                var open = h.OpenTime;
                var close = h.CloseTime;

                // handle overnight hours (close earlier than open)
                if (close <= open)
                {
                    if (currentTime >= open || currentTime < close)
                    {
                        shopOpen = true;
                        break;
                    }
                }
                else
                {
                    if (currentTime >= open && currentTime < close)
                    {
                        shopOpen = true;
                        break;
                    }
                }
            }

            // Override: allow force-close (e.g. holiday/day off) even if hours would be open
            var forceClosedSettingKey = $"Shop:{shopId.Value}:ForceClosed";
            var forceClosedSetting = await _db.AppSettings.FindAsync(forceClosedSettingKey);
            if (forceClosedSetting != null && bool.TryParse(forceClosedSetting.Value, out var forceClosed) && forceClosed)
            {
                shopOpen = false;
            }

            // Override: allow specifying a datetime until which the shop is closed.
            var closedUntilKey = $"Shop:{shopId.Value}:ClosedUntil";
            var closedUntilSetting = await _db.AppSettings.FindAsync(closedUntilKey);
            if (closedUntilSetting != null && DateTime.TryParse(closedUntilSetting.Value, out var closedUntil) && now < closedUntil)
            {
                shopOpen = false;
            }

            ViewBag.ShopOpen = shopOpen;
            ViewBag.ShopId = shopId.Value;

            // Compute a friendly status label (e.g., "Open - Closes 22:00" or "Closed - Opens 09:00")
            string statusLabel = shopOpen ? "Open" : "Closed";
            string statusTime = string.Empty;

            // Determine current active period (if any) and upcoming open time for today
            var todayPeriods = todaysHours
                .OrderBy(h => h.OpenTime)
                .ToList();

            if (shopOpen)
            {
                var activePeriod = todayPeriods.FirstOrDefault(h =>
                    (h.CloseTime > h.OpenTime && currentTime >= h.OpenTime && currentTime < h.CloseTime) ||
                    (h.CloseTime <= h.OpenTime && (currentTime >= h.OpenTime || currentTime < h.CloseTime))
                );

                if (activePeriod != null)
                {
                    var closeT = activePeriod.CloseTime;
                    statusTime = $"Closes {closeT:hh\\:mm}";
                }
            }
            else
            {
                var nextPeriod = todayPeriods.FirstOrDefault(h => h.OpenTime > currentTime);
                if (nextPeriod != null)
                {
                    statusTime = $"Opens {nextPeriod.OpenTime:hh\\:mm}";
                }
            }

            ViewBag.ShopStatusLabel = statusLabel;
            ViewBag.ShopStatusTime = statusTime;

            // Metrics
            var zoneVisits = 0;
            var narrationListens = 0;
            var avgDwellTimeSeconds = 0;
            var engagedSessions = 0;

            if (shopZoneIds.Count > 0)
            {
                zoneVisits = await _db.Analytics
                    .AsNoTracking()
                    .Where(a => a.ZoneId.HasValue
                        && shopZoneIds.Contains(a.ZoneId.Value)
                        && a.ActionType == "EnterZone")
                    .Select(a => a.SessionId)
                    .Distinct()
                    .CountAsync();

                narrationListens = await _db.Analytics
                    .AsNoTracking()
                    .Where(a => a.ZoneId.HasValue
                        && shopZoneIds.Contains(a.ZoneId.Value)
                        && a.ActionType.StartsWith("PlayNarration"))
                    .CountAsync();

                engagedSessions = await _db.Analytics
                    .AsNoTracking()
                    .Where(a => a.ZoneId.HasValue
                        && shopZoneIds.Contains(a.ZoneId.Value)
                        && a.ActionType.StartsWith("PlayNarration"))
                    .Select(a => a.SessionId)
                    .Distinct()
                    .CountAsync();

                var avgDwell = await _db.Analytics
                    .AsNoTracking()
                    .Where(a => a.ZoneId.HasValue
                        && shopZoneIds.Contains(a.ZoneId.Value)
                        && a.ActionType.StartsWith("PlayNarration"))
                    .Select(a => (double?)a.DwellTimeSeconds)
                    .AverageAsync();

                avgDwellTimeSeconds = avgDwell.HasValue ? (int)Math.Round(avgDwell.Value) : 0;
            }

            ViewBag.ZoneVisits = zoneVisits;
            ViewBag.NarrationListens = narrationListens;
            var rawEngagementRate = zoneVisits > 0
                ? Math.Round((double)engagedSessions / zoneVisits * 100, 0)
                : 0;
            ViewBag.EngagementRate = Math.Min(100, rawEngagementRate);
            ViewBag.AvgDwellTimeSeconds = avgDwellTimeSeconds;
            ViewBag.TotalPoIs = shopZones.Count;
            ViewBag.TotalAudioScripts = await _db.Narrations.CountAsync(n => shopZoneIds.Contains(n.ZoneId));

            // Weekly traffic trend (last 7 days, unique sessions entering zones per day)
            var todayUtc = DateTime.UtcNow.Date;
            var trendStart = todayUtc.AddDays(-6);
            var trendDays = Enumerable.Range(0, 7)
                .Select(i => trendStart.AddDays(i))
                .ToList();

            var dailyEnterEvents = new List<(DateTime Day, Guid SessionId)>();
            if (shopZoneIds.Count > 0)
            {
                var rawDailyEnterEvents = await _db.Analytics
                    .AsNoTracking()
                    .Where(a => a.ZoneId.HasValue
                        && shopZoneIds.Contains(a.ZoneId.Value)
                        && a.ActionType == "EnterZone"
                        && a.CreatedAt >= trendStart)
                    .Select(a => new { Day = a.CreatedAt.Date, a.SessionId })
                    .ToListAsync();

                dailyEnterEvents = rawDailyEnterEvents
                    .Select(x => (x.Day, x.SessionId))
                    .ToList();
            }

            var enterByDay = dailyEnterEvents
                .GroupBy(x => x.Day)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => x.SessionId).Distinct().Count());

            ViewBag.TrafficLabels = trendDays
                .Select(d => d.ToString("ddd").ToUpperInvariant())
                .ToList();
            ViewBag.TrafficValues = trendDays
                .Select(d => enterByDay.TryGetValue(d, out var count) ? count : 0)
                .ToList();

            // Language breakdown from PlayNarration events tagged as PlayNarration|<lang>
            var languagePlayCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (shopZoneIds.Count > 0)
            {
                var languageActions = await _db.Analytics
                    .AsNoTracking()
                    .Where(a => a.ZoneId.HasValue
                        && shopZoneIds.Contains(a.ZoneId.Value)
                        && a.ActionType.StartsWith("PlayNarration|"))
                    .Select(a => a.ActionType)
                    .ToListAsync();

                languagePlayCounts = languageActions
                    .Select(ExtractLanguageCode)
                    .Where(code => !string.IsNullOrWhiteSpace(code))
                    .GroupBy(code => code!, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);
            }

            var languages = new[]
            {
                new LanguageBreakdown("en", "English", "US", "#ef4444"),
                new LanguageBreakdown("vi", "Vietnamese", "VN", "#6b7280"),
                new LanguageBreakdown("ja", "Japanese", "JP", "#fca5a5")
            };

            var languageStats = languages
                .Select(l => new LanguageBreakdownViewModel
                {
                    Code = l.Code,
                    Name = l.Name,
                    CountryCode = l.CountryCode,
                    Color = l.Color,
                    Count = languagePlayCounts.TryGetValue(l.Code, out var count) ? count : 0
                })
                .ToList();

            var totalLanguageScans = languageStats.Sum(x => x.Count);
            foreach (var stat in languageStats)
            {
                stat.Percentage = totalLanguageScans > 0
                    ? Math.Round((double)stat.Count / totalLanguageScans * 100, 0)
                    : 0;
            }

            ViewBag.LanguageStats = languageStats;
            ViewBag.TotalLanguageScans = totalLanguageScans;

            return View();
        }

        private static string? ExtractLanguageCode(string actionType)
        {
            if (string.IsNullOrWhiteSpace(actionType))
            {
                return null;
            }

            var parts = actionType.Split('|', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || !parts[0].Equals("PlayNarration", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return parts[1].Trim().ToLowerInvariant();
        }

        private async Task<int?> ResolveCurrentVendorShopIdAsync()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(userIdClaim, out var userId))
            {
                var currentShopId = await _db.Users
                    .AsNoTracking()
                    .Where(u => u.Id == userId && u.Role == 1 && u.IsActive)
                    .Select(u => u.ShopId)
                    .FirstOrDefaultAsync();

                if (currentShopId.HasValue)
                {
                    return currentShopId;
                }
            }

            var username = User.Identity?.Name;
            if (!string.IsNullOrWhiteSpace(username))
            {
                var currentShopId = await _db.Users
                    .AsNoTracking()
                    .Where(u => u.Username == username && u.Role == 1 && u.IsActive)
                    .Select(u => u.ShopId)
                    .FirstOrDefaultAsync();

                if (currentShopId.HasValue)
                {
                    return currentShopId;
                }
            }

            // Last fallback for legacy sessions with stale claims.
            var shopIdClaim = User.FindFirst("ShopId")?.Value;
            if (int.TryParse(shopIdClaim, out var claimShopId))
            {
                return claimShopId;
            }

            return null;
        }

        private sealed record LanguageBreakdown(string Code, string Name, string CountryCode, string Color);

        public sealed class LanguageBreakdownViewModel
        {
            public string Code { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string CountryCode { get; set; } = string.Empty;
            public string Color { get; set; } = "#ef4444";
            public int Count { get; set; }
            public double Percentage { get; set; }
        }
    }
}
