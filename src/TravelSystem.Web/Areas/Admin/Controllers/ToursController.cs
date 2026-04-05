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
    public class ToursController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _env;
        private readonly IAudioTranslationService _translationService;

        public ToursController(AppDbContext db, IWebHostEnvironment env, IAudioTranslationService translationService)
        {
            _db = db;
            _env = env;
            _translationService = translationService;
        }

        // GET: Admin/Tours
        public async Task<IActionResult> Index()
        {
            var tours = await _db.Tours
                .Include(t => t.TourZones)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();
            return View(tours);
        }

        // GET: Admin/Tours/Create
        public async Task<IActionResult> Create()
        {
            ViewBag.Zones = await _db.Zones
                .Where(z => z.IsActive || z.Id == 0)
                .OrderBy(z => z.Name)
                .ToListAsync();
            return View(new Tour());
        }

        // POST: Admin/Tours/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Tour tour, int[] selectedZoneIds, IFormFile? imageFile)
        {
            if (ModelState.IsValid)
            {
                // Handle Image Upload
                if (imageFile != null)
                {
                    tour.ImageUrl = await Helpers.FileStorageHelper.SaveImageAsync(imageFile, _env.WebRootPath, "tours");
                }

                tour.CreatedAt = DateTime.UtcNow;
                tour.UpdatedAt = DateTime.UtcNow;
                _db.Tours.Add(tour);
                await _db.SaveChangesAsync(); // Get Tour Id

                var validZoneIds = await ResolveValidZoneIdsAsync(selectedZoneIds);
                if (validZoneIds.Count > 0)
                {
                    var selectedZones = await _db.Zones
                        .Where(z => validZoneIds.Contains(z.Id))
                        .ToDictionaryAsync(z => z.Id);

                    for (int i = 0; i < validZoneIds.Count; i++)
                    {
                        var zoneId = validZoneIds[i];
                        _db.TourZones.Add(new TourZone
                        {
                            TourId = tour.Id,
                            ZoneId = zoneId,
                            Zone = selectedZones.TryGetValue(zoneId, out var zone) ? zone : null,
                            OrderIndex = i + 1
                        });
                    }
                    await _db.SaveChangesAsync();
                }

                await UpsertTourTranslationsAsync(tour.Id, tour.Name, tour.Description);
                await _db.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }

            ViewBag.Zones = await _db.Zones
                .Where(z => z.IsActive || z.Id == 0)
                .OrderBy(z => z.Name)
                .ToListAsync();
            return View(tour);
        }

        // GET: Admin/Tours/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var tour = await _db.Tours
                .Include(t => t.TourZones)
                .ThenInclude(tz => tz.Zone)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (tour == null) return NotFound();

            ViewBag.Zones = await _db.Zones
                .Where(z => z.IsActive || z.Id == 0)
                .OrderBy(z => z.Name)
                .ToListAsync();
            return View(tour);
        }

        // POST: Admin/Tours/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Tour tour, int[] selectedZoneIds, IFormFile? imageFile)
        {
            if (id != tour.Id) return NotFound();

            if (ModelState.IsValid)
            {
                var dbTour = await _db.Tours
                    .Include(t => t.TourZones)
                    .FirstOrDefaultAsync(t => t.Id == id);
                    
                if (dbTour == null) return NotFound();

                // Handle Image Upload
                if (imageFile != null)
                {
                    // Delete old image if exists
                    if (!string.IsNullOrEmpty(dbTour.ImageUrl))
                    {
                        Helpers.FileStorageHelper.DeleteImage(dbTour.ImageUrl, _env.WebRootPath);
                    }
                    dbTour.ImageUrl = await Helpers.FileStorageHelper.SaveImageAsync(imageFile, _env.WebRootPath, "tours");
                }

                dbTour.Name = tour.Name;
                dbTour.Description = tour.Description;
                dbTour.Duration = tour.Duration;
                dbTour.UpdatedAt = DateTime.UtcNow;
                // Update Zones: Sử dụng so sánh để tránh xung đột khóa chính (Tracking collision)
                var currentTourZones = dbTour.TourZones.ToList();
                var selectedIds = await ResolveValidZoneIdsAsync(selectedZoneIds);

                // 1. Xóa các điểm không còn trong danh sách chọn
                foreach (var tz in currentTourZones)
                {
                    if (!selectedIds.Contains(tz.ZoneId))
                    {
                        dbTour.TourZones.Remove(tz);
                    }
                }

                // 2. Thêm hoặc cập nhật (thứ tự) các điểm đã chọn
                if (selectedIds.Count > 0)
                {
                    var selectedZones = await _db.Zones
                        .Where(z => selectedIds.Contains(z.Id))
                        .ToDictionaryAsync(z => z.Id);

                    for (int i = 0; i < selectedIds.Count; i++)
                    {
                        var zoneId = selectedIds[i];
                        var existing = currentTourZones.FirstOrDefault(tz => tz.ZoneId == zoneId);
                        
                        if (existing != null)
                        {
                            // Điểm đã tồn tại -> Chỉ cập nhật thứ tự
                            existing.OrderIndex = i + 1;
                        }
                        else
                        {
                            // Điểm mới -> Thêm vào collection
                            dbTour.TourZones.Add(new TourZone
                            {
                                ZoneId = zoneId,
                                Zone = selectedZones.TryGetValue(zoneId, out var zone) ? zone : null,
                                OrderIndex = i + 1
                            });
                        }
                    }
                }

                await _db.SaveChangesAsync();

                await UpsertTourTranslationsAsync(dbTour.Id, dbTour.Name, dbTour.Description);
                await _db.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }

            ViewBag.Zones = await _db.Zones
                .Where(z => z.IsActive || z.Id == 0)
                .OrderBy(z => z.Name)
                .ToListAsync();
            return View(tour);
        }

        // POST: Admin/Tours/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var tour = await _db.Tours.FindAsync(id);
            if (tour != null)
            {
                // Delete physical image file
                if (!string.IsNullOrEmpty(tour.ImageUrl))
                {
                    Helpers.FileStorageHelper.DeleteImage(tour.ImageUrl, _env.WebRootPath);
                }

                _db.Tours.Remove(tour);
                await _db.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        private async Task UpsertTourTranslationsAsync(int tourId, string sourceName, string? sourceDescription)
        {
            var supportedLanguages = new[] { "vi", "en", "ja", "ko" };
            var existing = await _db.TourTranslations
                .Where(tt => tt.TourId == tourId)
                .ToDictionaryAsync(tt => tt.Language);

            var sourceNameTrimmed = sourceName.Trim();
            var sourceDescriptionTrimmed = (sourceDescription ?? string.Empty).Trim();

            foreach (var language in supportedLanguages)
            {
                var translatedName = sourceName;
                var translatedDescription = sourceDescription;

                if (language != "vi")
                {
                    translatedName = await TranslateWithFallbackAsync(sourceName, language);
                    translatedDescription = await TranslateWithFallbackAsync(sourceDescription, language);

                    var translatedNameTrimmed = (translatedName ?? string.Empty).Trim();
                    var translatedDescriptionTrimmed = (translatedDescription ?? string.Empty).Trim();

                    if (existing.TryGetValue(language, out var currentExisting))
                    {
                        if (string.Equals(translatedNameTrimmed, sourceNameTrimmed, StringComparison.Ordinal)
                            && !string.IsNullOrWhiteSpace(currentExisting.Name))
                        {
                            translatedName = currentExisting.Name;
                        }

                        if (string.Equals(translatedDescriptionTrimmed, sourceDescriptionTrimmed, StringComparison.Ordinal)
                            && !string.IsNullOrWhiteSpace(currentExisting.Description))
                        {
                            translatedDescription = currentExisting.Description;
                        }
                    }
                    else
                    {
                        var looksLikeFallback = string.Equals(translatedNameTrimmed, sourceNameTrimmed, StringComparison.Ordinal)
                            && string.Equals(translatedDescriptionTrimmed, sourceDescriptionTrimmed, StringComparison.Ordinal);
                        if (looksLikeFallback)
                        {
                            continue;
                        }
                    }
                }

                if (existing.TryGetValue(language, out var current))
                {
                    current.Name = translatedName;
                    current.Description = translatedDescription;
                    current.UpdatedAt = DateTime.UtcNow;
                    continue;
                }

                _db.TourTranslations.Add(new TourTranslation
                {
                    TourId = tourId,
                    Language = language,
                    Name = translatedName,
                    Description = translatedDescription,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }
        }

        private async Task<string> TranslateWithFallbackAsync(string? sourceText, string targetLanguage)
        {
            if (string.IsNullOrWhiteSpace(sourceText))
            {
                return string.Empty;
            }

            try
            {
                return await _translationService.TranslateAsync(sourceText, targetLanguage);
            }
            catch
            {
                return sourceText;
            }
        }

        private async Task<List<int>> ResolveValidZoneIdsAsync(IEnumerable<int>? zoneIds)
        {
            var normalizedIds = zoneIds?
                .Where(id => id >= 0)
                .Distinct()
                .ToList() ?? new List<int>();

            if (normalizedIds.Count == 0)
            {
                return new List<int>();
            }

            var existingIds = await _db.Zones
                .Where(z => normalizedIds.Contains(z.Id) && (z.IsActive || z.Id == 0))
                .Select(z => z.Id)
                .ToHashSetAsync();

            return normalizedIds.Where(existingIds.Contains).ToList();
        }
    }
}
