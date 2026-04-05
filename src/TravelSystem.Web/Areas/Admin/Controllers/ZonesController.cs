using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TravelSystem.Shared.Models;
using TravelSystem.Web.Data;
using TravelSystem.Web.Services;

namespace TravelSystem.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(AuthenticationSchemes = "AdminAuth", Roles = "Admin")]
    public class ZonesController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _env;
        private readonly IAudioTranslationService _translationService;

        public ZonesController(AppDbContext db, IWebHostEnvironment env, IAudioTranslationService translationService)
        {
            _db = db;
            _env = env;
            _translationService = translationService;
        }

        // GET: Admin/Zones
        public async Task<IActionResult> Index(int page = 1)
        {
            const int pageSize = 20;
            var query = _db.Zones.AsQueryable();

            var totalCount = await query.CountAsync();
            var totalPages = (int) Math.Ceiling(totalCount / (double)pageSize);
            ViewBag.Page = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalCount = totalCount;
            ViewBag.PageSize = pageSize;

            var zones = await query
                .OrderBy(z => z.OrderIndex)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Count narrations for each zone dynamically just for view mockups
            ViewBag.NarrationCounts = await _db.Narrations
                .GroupBy(n => n.ZoneId)
                .ToDictionaryAsync(g => g.Key, g => g.Count());

            return View(zones);
        }

        public async Task<IActionResult> Create()
        {
            ViewBag.Shops = _db.Shops.ToList();
            ViewBag.ExistingZones = await _db.Zones.Where(z => z.IsActive).ToListAsync();
            return View(new Zone { IsActive = true });
        }

        // POST: Admin/Zones/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Zone zone, IFormFile? imageFile)
        {
            if (ModelState.IsValid)
            {
                // Handle Image Upload
                if (imageFile != null)
                {
                    zone.ImageUrl = await Helpers.FileStorageHelper.SaveImageAsync(imageFile, _env.WebRootPath, "zones");
                }

                zone.CreatedAt = DateTime.UtcNow;
                zone.UpdatedAt = DateTime.UtcNow;
                zone.IsLocked = false;
                zone.IsHidden = false;

                _db.Zones.Add(zone);
                await _db.SaveChangesAsync();

                await UpsertZoneTranslationsAsync(zone.Id, zone.Description);
                await _db.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }

            ViewBag.Shops = _db.Shops.ToList();
            return View(zone);
        }

        // GET: Admin/Zones/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var zone = await _db.Zones.FindAsync(id);
            if (zone == null) return NotFound();

            if (zone.IsLocked)
            {
                ModelState.AddModelError("", $"Zone này đang bị khóa. Lý do: {zone.LockReason}");
            }

            ViewBag.Shops = _db.Shops.ToList();
            return View(zone);
        }

        // POST: Admin/Zones/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Zone zone, IFormFile? imageFile)
        {
            if (id != zone.Id) return NotFound();

            var dbZone = await _db.Zones.FindAsync(id);
            if (dbZone == null) return NotFound();

            // Kiểm tra nếu zone bị khóa
            if (dbZone.IsLocked)
            {
                ModelState.AddModelError("", $"Không thể chỉnh sửa. Zone này đang bị khóa. Lý do: {dbZone.LockReason}");
                ViewBag.Shops = _db.Shops.ToList();
                return View(zone);
            }

            if (ModelState.IsValid)
            {
                var shouldRefreshTranslations = !string.Equals(dbZone.Description, zone.Description, StringComparison.Ordinal);

                // Handle Image Upload
                if (imageFile != null)
                {
                    // Delete old image if exists
                    if (!string.IsNullOrEmpty(dbZone.ImageUrl))
                    {
                        Helpers.FileStorageHelper.DeleteImage(dbZone.ImageUrl, _env.WebRootPath);
                    }
                    dbZone.ImageUrl = await Helpers.FileStorageHelper.SaveImageAsync(imageFile, _env.WebRootPath, "zones");
                }

                dbZone.Name = zone.Name;
                dbZone.Description = zone.Description;
                dbZone.Latitude = zone.Latitude;
                dbZone.Longitude = zone.Longitude;
                dbZone.Radius = zone.Radius;
                dbZone.ZoneType = zone.ZoneType;
                dbZone.ShopId = zone.ShopId;
                dbZone.OrderIndex = zone.OrderIndex;
                dbZone.IsActive = zone.IsActive;
                dbZone.IsMain = zone.IsMain;
                dbZone.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();

                if (shouldRefreshTranslations)
                {
                    await UpsertZoneTranslationsAsync(dbZone.Id, dbZone.Description);
                    await _db.SaveChangesAsync();
                }

                return RedirectToAction(nameof(Index));
            }

            ViewBag.Shops = _db.Shops.ToList();
            return View(zone);
        }

        // POST: Admin/Zones/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var zone = await _db.Zones.FindAsync(id);
            if (zone != null)
            {
                // Delete physical image file
                if (!string.IsNullOrEmpty(zone.ImageUrl))
                {
                    Helpers.FileStorageHelper.DeleteImage(zone.ImageUrl, _env.WebRootPath);
                }

                // Analytics vẫn cần giữ lại lịch sử nên tách FK trước khi xóa zone.
                var analyticsRows = await _db.Analytics
                    .Where(a => a.ZoneId == id)
                    .ToListAsync();

                if (analyticsRows.Count > 0)
                {
                    foreach (var row in analyticsRows)
                    {
                        row.ZoneId = null;
                    }
                }

                _db.Zones.Remove(zone);
                await _db.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        // POST: Admin/Zones/Lock/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Lock(int id, string reason = "")
        {
            var zone = await _db.Zones.FindAsync(id);
            if (zone != null)
            {
                zone.IsLocked = true;
                zone.LockReason = reason ?? "Lý do không được cung cấp";
                zone.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        // POST: Admin/Zones/Unlock/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Unlock(int id)
        {
            var zone = await _db.Zones.FindAsync(id);
            if (zone != null)
            {
                zone.IsLocked = false;
                zone.LockReason = null;
                zone.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        // POST: Admin/Zones/Hide/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Hide(int id)
        {
            var zone = await _db.Zones.FindAsync(id);
            if (zone != null)
            {
                zone.IsHidden = true;
                zone.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        // POST: Admin/Zones/Show/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Show(int id)
        {
            var zone = await _db.Zones.FindAsync(id);
            if (zone != null)
            {
                zone.IsHidden = false;
                zone.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        private async Task UpsertZoneTranslationsAsync(int zoneId, string? vietnameseDescription)
        {
            var sourceText = (vietnameseDescription ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(sourceText))
            {
                return;
            }

            var targetLanguages = new[] { "vi", "en", "ja", "ko" };
            foreach (var language in targetLanguages)
            {
                var existingRecords = await _db.ZoneTranslations
                    .Where(t => t.ZoneId == zoneId && t.Language == language)
                    .OrderByDescending(t => t.UpdatedAt)
                    .ThenByDescending(t => t.Id)
                    .ToListAsync();

                var translatedText = language == "vi"
                    ? sourceText
                    : await TranslateWithFallbackAsync(sourceText, language);

                if (language != "vi"
                    && string.Equals(translatedText.Trim(), sourceText.Trim(), StringComparison.Ordinal)
                    && existingRecords.Count > 0
                    && !string.IsNullOrWhiteSpace(existingRecords[0].Description))
                {
                    translatedText = existingRecords[0].Description;
                }

                if (language != "vi"
                    && existingRecords.Count == 0
                    && string.Equals(translatedText.Trim(), sourceText.Trim(), StringComparison.Ordinal))
                {
                    continue;
                }

                ZoneTranslation record;
                if (existingRecords.Count == 0)
                {
                    record = new ZoneTranslation
                    {
                        ZoneId = zoneId,
                        Language = language
                    };
                    _db.ZoneTranslations.Add(record);
                }
                else
                {
                    record = existingRecords[0];
                    if (existingRecords.Count > 1)
                    {
                        _db.ZoneTranslations.RemoveRange(existingRecords.Skip(1));
                    }
                }

                record.Description = translatedText;
                record.UpdatedAt = DateTime.UtcNow;
            }
        }

        private async Task<string> TranslateWithFallbackAsync(string sourceText, string targetLanguage)
        {
            try
            {
                return await _translationService.TranslateAsync(sourceText, targetLanguage);
            }
            catch
            {
                return sourceText;
            }
        }
    }
}

