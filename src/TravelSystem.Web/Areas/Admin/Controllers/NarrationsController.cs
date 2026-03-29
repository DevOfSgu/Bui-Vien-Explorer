using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TravelSystem.Web.Data;

namespace TravelSystem.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(AuthenticationSchemes = "AdminAuth", Roles = "Admin")]
    public class NarrationsController : Controller
    {
        private readonly AppDbContext _db;
        private readonly TravelSystem.Web.Services.IAudioTranslationService _audioService;
        private readonly IWebHostEnvironment _env;

        public NarrationsController(AppDbContext db, TravelSystem.Web.Services.IAudioTranslationService audioService, IWebHostEnvironment env)
        {
            _db = db;
            _audioService = audioService;
            _env = env;
        }

        public async Task<IActionResult> Index(int? zoneId, int page = 1)
        {
            const int pageSize = 20;
            var query = _db.Narrations.AsQueryable();

            if (zoneId.HasValue)
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
            TempData["Error"] = "Editing is disabled in Admin. You can review, approve or reject only.";
            return RedirectToAction(nameof(Details), new { id = id.Value });
        }

        // POST: Admin/Narrations/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, TravelSystem.Shared.Models.Narration narration)
        {
            TempData["Error"] = "Editing is disabled in Admin. Ask vendor to resubmit changes.";
            return RedirectToAction(nameof(Index));
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
        public async Task<IActionResult> Approve(int id)
        {
            var narration = await _db.Narrations.FindAsync(id);
            if (narration != null && narration.ApprovalStatus != "Approved")
            {
                narration.ApprovalStatus = "Approved";
                narration.UpdatedAt = DateTime.UtcNow;

                // Generate TTS for original language
                narration.FileUrl = await _audioService.GenerateTtsAsync(narration.Text, narration.Language, narration.ZoneId, _env.WebRootPath);
                narration.AudioStatus = "ready";

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
            }
            return RedirectToAction(nameof(Index), new { zoneId = narration?.ZoneId });
        }

        // POST: Admin/Narrations/Reject/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id)
        {
            var narration = await _db.Narrations.FindAsync(id);
            if (narration != null)
            {
                narration.ApprovalStatus = "Rejected";
                await _db.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index), new { zoneId = narration?.ZoneId });
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

            existing.FileUrl = await _audioService.GenerateTtsAsync(text, language, zoneId, _env.WebRootPath);
            existing.AudioStatus = "ready";
        }
    }
}
