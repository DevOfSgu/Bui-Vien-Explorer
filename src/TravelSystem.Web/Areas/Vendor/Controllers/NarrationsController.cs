using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TravelSystem.Web.Data;

namespace TravelSystem.Web.Areas.Vendor.Controllers
{
    [Area("Vendor")]
    [Authorize(AuthenticationSchemes = "VendorAuth", Roles = "Vendor")]
    public class NarrationsController : Controller
    {
        private readonly AppDbContext _db;
        private readonly TravelSystem.Web.Services.INotificationService _notificationService;

        public NarrationsController(AppDbContext db, TravelSystem.Web.Services.INotificationService notificationService)
        {
            _db = db;
            _notificationService = notificationService;
        }

        public async Task<IActionResult> Index(int? zoneId, int page = 1)
        {
            const int pageSize = 20;
            var vendorShopId = await GetVendorShopIdAsync();
            if (vendorShopId == null) return Challenge("VendorAuth");

            var vendorZoneIds = await _db.Zones
                .Where(z => z.ShopId == vendorShopId)
                .Select(z => z.Id)
                .ToListAsync();

            await NormalizeLegacySourceNarrationsAsync(vendorZoneIds);

            // Vendor manages source scripts only (Vietnamese).
            var query = _db.Narrations
                .Where(n => vendorZoneIds.Contains(n.ZoneId) && n.Language == "vi")
                .AsQueryable();

            if (zoneId.HasValue && vendorZoneIds.Contains(zoneId.Value))
            {
                query = query.Where(n => n.ZoneId == zoneId.Value);
                ViewBag.SelectedZoneId = zoneId;
                ViewBag.ZoneName = _db.Zones.Find(zoneId.Value)?.Name;
            }

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalCount = totalCount;
            ViewBag.TotalPages = totalPages;

            var narrations = await query.OrderBy(n => n.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.ZonesMap = await _db.Zones
                .Where(z => vendorZoneIds.Contains(z.Id))
                .ToDictionaryAsync(z => z.Id, z => z.Name);

            return View(narrations);
        }

        // GET: Vendor/Narrations/Create
        public async Task<IActionResult> Create(int? zoneId)
        {
            var vendorShopId = await GetVendorShopIdAsync();
            if (vendorShopId == null) return Challenge("VendorAuth");

            var vendorZoneIds = await _db.Zones
                .Where(z => z.ShopId == vendorShopId)
                .Select(z => z.Id)
                .ToListAsync();
            await NormalizeLegacySourceNarrationsAsync(vendorZoneIds);

            var availableZones = await _db.Zones
                .Where(z => z.ShopId == vendorShopId && z.IsActive && !z.IsHidden)
                .Where(z => !_db.Narrations.Any(n => n.ZoneId == z.Id))
                .OrderBy(z => z.Name)
                .ToListAsync();

            ViewBag.Zones = availableZones;
            ViewBag.NoAvailableZone = availableZones.Count == 0;
            return View(new TravelSystem.Shared.Models.Narration { ZoneId = zoneId ?? 0 });
        }

        // POST: Vendor/Narrations/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(TravelSystem.Shared.Models.Narration narration)
        {
            // Ensure vendor owns the zone
            var vendorShopId = await GetVendorShopIdAsync();
            if (vendorShopId == null) return Challenge("VendorAuth");

            await NormalizeLegacySourceNarrationsAsync(new List<int> { narration.ZoneId });

            var zone = await _db.Zones.FindAsync(narration.ZoneId);
            if (zone == null || zone.ShopId != vendorShopId) return Unauthorized();
            if (!zone.IsActive || zone.IsHidden) return Unauthorized();

            var hasExistingScript = await _db.Narrations.AnyAsync(n => n.ZoneId == narration.ZoneId);
            if (hasExistingScript)
            {
                TempData["Error"] = "This POI already has a script. Please use Edit/Resubmit instead of adding a new one.";
                return RedirectToAction(nameof(Index), new { zoneId = narration.ZoneId });
            }

            // Set vendor specific data
            narration.Language = "vi";
            narration.UpdatedBy = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            narration.UpdatedAt = DateTime.UtcNow;
            narration.ApprovalStatus = "Pending"; // Vendor creations are always pending
            narration.AudioStatus = "pending";

            _db.Narrations.Add(narration);
            await _db.SaveChangesAsync();

            var zoneName = await _db.Zones
                .Where(z => z.Id == narration.ZoneId)
                .Select(z => z.Name)
                .FirstOrDefaultAsync() ?? $"Zone #{narration.ZoneId}";

            await _notificationService.NotifyAdminsAsync(
                $"Vendor đã tạo script mới cho khu vực: {zoneName}",
                Url.Action("Index", "Narrations", new { area = "Admin", zoneId = narration.ZoneId }));

            TempData["Success"] = "Script submitted for admin approval.";
            return RedirectToAction(nameof(Index), new { zoneId = narration.ZoneId });
        }

        // GET: Vendor/Narrations/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var narration = await _db.Narrations.FindAsync(id);
            if (narration == null) return NotFound();

            // Ensure vendor owns the zone
            var vendorShopId = await GetVendorShopIdAsync();
            if (vendorShopId == null) return Challenge("VendorAuth");

            var zone = await _db.Zones.FindAsync(narration.ZoneId);
            if (zone == null || zone.ShopId != vendorShopId) return Unauthorized();

            await NormalizeLegacySourceNarrationsAsync(new List<int> { narration.ZoneId });
            narration = await _db.Narrations.FindAsync(id);
            if (narration == null) return NotFound();
            if (narration.Language != "vi") return Unauthorized();

            ViewBag.Zones = await _db.Zones.Where(z => z.ShopId == vendorShopId).ToListAsync();
            return View(narration);
        }

        // POST: Vendor/Narrations/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, TravelSystem.Shared.Models.Narration narration)
        {
            if (id != narration.Id) return NotFound();

            // Ensure vendor owns the script/zone and only allow resubmitting source (vi) script.
            var vendorShopId = await GetVendorShopIdAsync();
            if (vendorShopId == null) return Challenge("VendorAuth");

            await NormalizeLegacySourceNarrationsAsync(new List<int> { narration.ZoneId });

            var dbNarration = await _db.Narrations.FindAsync(id);
            if (dbNarration != null)
            {
                var zone = await _db.Zones.FindAsync(dbNarration.ZoneId);
                if (zone == null || zone.ShopId != vendorShopId) return Unauthorized();
                if (dbNarration.Language != "vi") return Unauthorized();

                dbNarration.Text = narration.Text;
                dbNarration.VoiceId = narration.VoiceId;
                dbNarration.UpdatedBy = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                dbNarration.UpdatedAt = DateTime.UtcNow;
                dbNarration.ApprovalStatus = "Pending"; // Vendor edits reset status to Pending
                dbNarration.AudioStatus = "pending";
                await _db.SaveChangesAsync();

                var zoneName = await _db.Zones
                    .Where(z => z.Id == dbNarration.ZoneId)
                    .Select(z => z.Name)
                    .FirstOrDefaultAsync() ?? $"Zone #{dbNarration.ZoneId}";

                await _notificationService.NotifyAdminsAsync(
                    $"Vendor đã gửi lại script cần duyệt cho khu vực: {zoneName}",
                    Url.Action("Index", "Narrations", new { area = "Admin", zoneId = dbNarration.ZoneId }));

                TempData["Success"] = "Updated script submitted for admin approval.";
            }

            return RedirectToAction(nameof(Index), new { zoneId = dbNarration?.ZoneId });
        }

        private async Task<int?> GetVendorShopIdAsync()
        {
            // Resolve from DB first so vendor-shop reassignments take effect without requiring re-login.
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
            if (string.IsNullOrWhiteSpace(username))
            {
                var shopIdClaim = User.FindFirst("ShopId")?.Value;
                if (int.TryParse(shopIdClaim, out var shopIdFromClaim))
                {
                    return shopIdFromClaim;
                }

                return null;
            }

            var vendor = await _db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Username == username && u.Role == 1 && u.IsActive);

            if (vendor?.ShopId != null)
            {
                return vendor.ShopId;
            }

            // Last fallback for legacy sessions.
            var fallbackShopIdClaim = User.FindFirst("ShopId")?.Value;
            if (int.TryParse(fallbackShopIdClaim, out var fallbackShopId))
            {
                return fallbackShopId;
            }

            return null;
        }

        private async Task NormalizeLegacySourceNarrationsAsync(IReadOnlyCollection<int> zoneIds)
        {
            if (zoneIds.Count == 0) return;

            var rows = await _db.Narrations
                .Where(n => zoneIds.Contains(n.ZoneId))
                .ToListAsync();

            if (rows.Count == 0) return;

            var changed = false;
            foreach (var group in rows.GroupBy(n => n.ZoneId))
            {
                if (group.Any(n => n.Language == "vi"))
                {
                    continue;
                }

                var legacySource = group
                    .Where(n => n.Language == "en" || n.Language == "ja" || n.Language == "ko")
                    .OrderByDescending(n => n.Language == "en" ? 3 : n.Language == "ja" ? 2 : 1)
                    .ThenByDescending(n => n.UpdatedAt)
                    .ThenByDescending(n => n.Id)
                    .FirstOrDefault();

                if (legacySource == null)
                {
                    continue;
                }

                legacySource.Language = "vi";
                legacySource.UpdatedAt = DateTime.UtcNow;
                changed = true;
            }

            if (changed)
            {
                await _db.SaveChangesAsync();
            }
        }
    }
}
