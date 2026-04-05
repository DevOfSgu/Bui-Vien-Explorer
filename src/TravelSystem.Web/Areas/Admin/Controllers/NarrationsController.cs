using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TravelSystem.Web.Data;

namespace TravelSystem.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(AuthenticationSchemes = "AdminAuth", Roles = "Admin")]
    public class NarrationsController : Controller
    {
        private readonly AppDbContext _db;
        private readonly TravelSystem.Web.Services.IAudioTranslationService _audioService;
        private readonly TravelSystem.Web.Services.INotificationService _notificationService;
        private readonly IWebHostEnvironment _env;

        public NarrationsController(AppDbContext db, TravelSystem.Web.Services.IAudioTranslationService audioService, TravelSystem.Web.Services.INotificationService notificationService, IWebHostEnvironment env)
        {
            _db = db;
            _audioService = audioService;
            _notificationService = notificationService;
            _env = env;
        }

        public async Task<IActionResult> Index(int? zoneId, string? language, int page = 1)
        {
            const int pageSize = 20;
            var query = _db.Narrations.AsQueryable();

            if (zoneId.HasValue)
            {
                query = query.Where(n => n.ZoneId == zoneId.Value);
                ViewBag.SelectedZoneId = zoneId;
                ViewBag.ZoneName = _db.Zones.Find(zoneId.Value)?.Name;
            }

            var normalizedLanguage = NormalizeLanguage(language);
            if (!string.IsNullOrWhiteSpace(normalizedLanguage) && normalizedLanguage != "all")
            {
                query = query.Where(n => n.Language != null && n.Language.ToLower().StartsWith(normalizedLanguage));
            }

            ViewBag.SelectedLanguage = string.IsNullOrWhiteSpace(normalizedLanguage) ? "all" : normalizedLanguage;

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
            
            ViewBag.ZonesMap = await _db.Zones.ToDictionaryAsync(z => z.Id, z => z.Name);
            
            return View(narrations);
        }

        // GET: Admin/Narrations/Create
        public IActionResult Create(int? zoneId)
        {
            ViewBag.Zones = _db.Zones.ToList();
            return View(new TravelSystem.Shared.Models.Narration { ZoneId = zoneId ?? 0 });
        }

        // POST: Admin/Narrations/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(TravelSystem.Shared.Models.Narration narration)
        {
            narration.UpdatedAt = DateTime.UtcNow;
            narration.ApprovalStatus = "Approved"; // Admin creations are auto-approved
            
            _db.Narrations.Add(narration);
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index), new { zoneId = narration.ZoneId });
        }

        // GET: Admin/Narrations/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var narration = await _db.Narrations.FindAsync(id.Value);
            if (narration == null) return NotFound();

            if (!string.Equals(narration.ApprovalStatus, "Approved", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "Chỉ được chỉnh sửa sau khi bản ghi đã được duyệt.";
                return RedirectToAction(nameof(Details), new { id = id.Value });
            }

            ViewBag.Zones = await _db.Zones
                .OrderBy(z => z.Name)
                .ToListAsync();

            return View(narration);
        }

        // GET: Admin/Narrations/Manage?zoneId=5
        public async Task<IActionResult> Manage(int zoneId)
        {
            var zone = await _db.Zones
                .AsNoTracking()
                .FirstOrDefaultAsync(z => z.Id == zoneId);

            if (zone == null)
            {
                return NotFound();
            }

            var zoneNarrations = await _db.Narrations
                .AsNoTracking()
                .Where(n => n.ZoneId == zoneId)
                .OrderByDescending(n => n.UpdatedAt)
                .ThenByDescending(n => n.Id)
                .ToListAsync();

            var preferredLanguages = new[]
            {
                (Code: "vi", Name: "Tiếng Việt"),
                (Code: "en", Name: "Tiếng Anh"),
                (Code: "ja", Name: "Tiếng Nhật")
            };

            var items = preferredLanguages
                .Select(lang =>
                {
                    var narration = zoneNarrations
                        .FirstOrDefault(n => NormalizeLanguage(n.Language) == lang.Code);

                    return new NarrationLanguageEditItem
                    {
                        ZoneId = zoneId,
                        ZoneName = zone.Name,
                        LanguageCode = lang.Code,
                        LanguageName = lang.Name,
                        NarrationId = narration?.Id,
                        ApprovalStatus = narration?.ApprovalStatus ?? "NotCreated",
                        AudioStatus = narration?.AudioStatus ?? "pending"
                    };
                })
                .ToList();

            return View(items);
        }

        // POST: Admin/Narrations/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, TravelSystem.Shared.Models.Narration narration)
        {
            if (id != narration.Id)
            {
                return NotFound();
            }

            var existing = await _db.Narrations.FindAsync(id);
            if (existing == null)
            {
                return NotFound();
            }

            if (!string.Equals(existing.ApprovalStatus, "Approved", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "Chỉ được chỉnh sửa sau khi bản ghi đã được duyệt.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (string.IsNullOrWhiteSpace(narration.Text))
            {
                ModelState.AddModelError(nameof(narration.Text), "Nội dung thuyết minh không được để trống.");
            }

            var normalizedLanguage = NormalizeLanguage(existing.Language);
            var duplicate = await _db.Narrations
                .AsNoTracking()
                .Where(n => n.Id != id && n.ZoneId == narration.ZoneId && n.Language == normalizedLanguage)
                .FirstOrDefaultAsync();

            if (duplicate != null)
            {
                ModelState.AddModelError(string.Empty, "Đã tồn tại bản thuyết minh cho khu vực và ngôn ngữ này. Vui lòng sửa trực tiếp bản hiện có.");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Zones = await _db.Zones
                    .OrderBy(z => z.Name)
                    .ToListAsync();
                return View(narration);
            }

            existing.ZoneId = narration.ZoneId;
            existing.Language = normalizedLanguage;
            existing.Text = narration.Text.Trim();
            existing.VoiceId = narration.VoiceId;
            existing.ApprovalStatus = "Approved";
            existing.AudioStatus = "pending";
            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedBy = GetCurrentAdminUserId();

            try
            {
                await GenerateAndApplyAudioAsync(existing, existing.Text, existing.Language, existing.ZoneId);
            }
            catch
            {
                existing.AudioStatus = "error";
                existing.FileUrl = null;
                TempData["Error"] = "Cập nhật nội dung thành công nhưng tạo audio thất bại. Vui lòng thử lại.";
            }

            await _db.SaveChangesAsync();
            if (TempData["Error"] == null)
            {
                TempData["Success"] = "Đã cập nhật ngôn ngữ và tạo lại audio thành công.";
            }

            return RedirectToAction(nameof(Index), new { zoneId = existing.ZoneId });
        }

        // GET: Admin/Narrations/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var narration = await _db.Narrations.FindAsync(id);
            if (narration == null) return NotFound();

            ViewBag.ZoneName = await _db.Zones
                .Where(z => z.Id == narration.ZoneId)
                .Select(z => z.Name)
                .FirstOrDefaultAsync();

            return View(narration);
        }

        // POST: Admin/Narrations/Approve/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id, string? returnUrl = null)
        {
            var narration = await _db.Narrations.FindAsync(id);
            if (narration != null && narration.ApprovalStatus != "Approved")
            {
                narration.ApprovalStatus = "Approved";
                narration.UpdatedAt = DateTime.UtcNow;

                // Generate TTS for original language
                await GenerateAndApplyAudioAsync(narration, narration.Text, narration.Language, narration.ZoneId);

                if (narration.Language.ToLower() == "vi")
                {
                    // Upsert English translation/audio
                    var enText = await _audioService.TranslateAsync(narration.Text, "en");
                    await UpsertTranslatedNarrationAsync(narration.ZoneId, "en", enText);

                    // Upsert Japanese translation/audio
                    var jaText = await _audioService.TranslateAsync(narration.Text, "ja");
                    await UpsertTranslatedNarrationAsync(narration.ZoneId, "ja", jaText);
                }

                await _db.SaveChangesAsync();

                var vendorUserId = await ResolveVendorUserIdForZoneAsync(narration.ZoneId, narration.UpdatedBy);
                if (vendorUserId is > 0)
                {
                    await NotifyVendorNarrationDecisionAsync(vendorUserId.Value, narration.ZoneId, "đã được duyệt");
                }

                TempData["Success"] = "Đã duyệt thành công.";
            }
            else if (narration != null)
            {
                TempData["Success"] = "Bản ghi đã ở trạng thái duyệt.";
            }

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction(nameof(Index), new { zoneId = narration?.ZoneId });
        }

        // POST: Admin/Narrations/Reject/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id, string? returnUrl = null)
        {
            var narration = await _db.Narrations.FindAsync(id);
            if (narration != null)
            {
                narration.ApprovalStatus = "Rejected";
                await _db.SaveChangesAsync();

                var vendorUserId = await ResolveVendorUserIdForZoneAsync(narration.ZoneId, narration.UpdatedBy);
                if (vendorUserId is > 0)
                {
                    await NotifyVendorNarrationDecisionAsync(vendorUserId.Value, narration.ZoneId, "bị từ chối");
                }

                TempData["Success"] = "Đã từ chối bản ghi.";
            }

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction(nameof(Index), new { zoneId = narration?.ZoneId });
        }

        private async Task<int?> ResolveVendorUserIdForZoneAsync(int zoneId, int? preferredVendorUserId)
        {
            if (preferredVendorUserId is > 0)
            {
                var preferredIsValid = await _db.Users
                    .AsNoTracking()
                    .AnyAsync(u => u.Id == preferredVendorUserId.Value && u.Role == 1 && u.IsActive);

                if (preferredIsValid)
                {
                    return preferredVendorUserId.Value;
                }
            }

            var shopId = await _db.Zones
                .AsNoTracking()
                .Where(z => z.Id == zoneId)
                .Select(z => z.ShopId)
                .FirstOrDefaultAsync();

            if (!shopId.HasValue)
            {
                return null;
            }

            return await _db.Users
                .AsNoTracking()
                .Where(u => u.Role == 1 && u.IsActive && u.ShopId == shopId.Value)
                .OrderBy(u => u.Id)
                .Select(u => (int?)u.Id)
                .FirstOrDefaultAsync();
        }

        private async Task NotifyVendorNarrationDecisionAsync(int vendorUserId, int zoneId, string decisionText)
        {
            var isActiveVendor = await _db.Users
                .AsNoTracking()
                .AnyAsync(u => u.Id == vendorUserId && u.Role == 1 && u.IsActive);

            if (!isActiveVendor)
            {
                return;
            }

            var zoneName = await _db.Zones
                .Where(z => z.Id == zoneId)
                .Select(z => z.Name)
                .FirstOrDefaultAsync() ?? $"Zone #{zoneId}";

            await _notificationService.NotifyVendorAsync(
                vendorUserId,
                $"Audio script cho '{zoneName}' {decisionText}.",
                Url.Action("Index", "Narrations", new { area = "Vendor", zoneId }));
        }

        private async Task UpsertTranslatedNarrationAsync(int zoneId, string language, string text)
        {
            var existing = await _db.Narrations
                .Where(n => n.ZoneId == zoneId && n.Language == language)
                .OrderByDescending(n => n.UpdatedAt)
                .ThenByDescending(n => n.Id)
                .FirstOrDefaultAsync();

            if (existing == null)
            {
                existing = new TravelSystem.Shared.Models.Narration
                {
                    ZoneId = zoneId,
                    Language = language,
                    Text = text,
                    ApprovalStatus = "Approved",
                    AudioStatus = "pending",
                    UpdatedAt = DateTime.UtcNow
                };
                _db.Narrations.Add(existing);
                await _db.SaveChangesAsync(); // ensure Id before generating file naming
            }
            else
            {
                existing.Text = text;
                existing.ApprovalStatus = "Approved";
                existing.AudioStatus = "pending";
                existing.UpdatedAt = DateTime.UtcNow;
            }

            await GenerateAndApplyAudioAsync(existing, text, language, zoneId);
        }

        private async Task GenerateAndApplyAudioAsync(TravelSystem.Shared.Models.Narration narration, string text, string language, int zoneId)
        {
            var fileUrl = await _audioService.GenerateTtsAsync(text, language, zoneId, _env.WebRootPath);

            if (string.IsNullOrWhiteSpace(fileUrl))
            {
                narration.AudioStatus = "error";
                narration.FileUrl = null;
                return;
            }

            narration.FileUrl = fileUrl;
            narration.AudioStatus = "ready";
        }

        private static string NormalizeLanguage(string? language)
        {
            if (string.IsNullOrWhiteSpace(language))
            {
                return "vi";
            }

            var normalized = language.Trim().ToLowerInvariant();
            var dashIndex = normalized.IndexOf('-');
            if (dashIndex > 0)
            {
                normalized = normalized[..dashIndex];
            }

            return normalized.Length > 5 ? normalized[..5] : normalized;
        }

        private int? GetCurrentAdminUserId()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(userIdClaim, out var userId) ? userId : null;
        }

        public sealed class NarrationLanguageEditItem
        {
            public int ZoneId { get; set; }
            public string ZoneName { get; set; } = string.Empty;
            public int? NarrationId { get; set; }
            public string LanguageCode { get; set; } = string.Empty;
            public string LanguageName { get; set; } = string.Empty;
            public string ApprovalStatus { get; set; } = string.Empty;
            public string AudioStatus { get; set; } = string.Empty;
        }
    }
}
