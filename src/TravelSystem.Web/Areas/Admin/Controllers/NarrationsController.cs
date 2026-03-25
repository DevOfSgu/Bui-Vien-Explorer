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

            var narration = await _db.Narrations.FindAsync(id);
            if (narration == null) return NotFound();

            ViewBag.Zones = _db.Zones.ToList();
            return View(narration);
        }

        // POST: Admin/Narrations/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, TravelSystem.Shared.Models.Narration narration)
        {
            if (id != narration.Id) return NotFound();

            var dbNarration = await _db.Narrations.FindAsync(id);
            if(dbNarration != null) {
                dbNarration.ZoneId = narration.ZoneId;
                dbNarration.Language = narration.Language;
                dbNarration.Text = narration.Text;
                dbNarration.VoiceId = narration.VoiceId;
                dbNarration.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index), new { zoneId = narration.ZoneId });
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

                // Generate TTS for original language
                narration.FileUrl = await _audioService.GenerateTtsAsync(narration.Text, narration.Language, narration.Id, _env.WebRootPath);
                narration.AudioStatus = "ready";

                if (narration.Language.ToLower() == "vi")
                {
                    // Generate English Translation and Audio
                    var enText = await _audioService.TranslateAsync(narration.Text, "en");
                    var enNarration = new TravelSystem.Shared.Models.Narration
                    {
                        ZoneId = narration.ZoneId,
                        Language = "en",
                        Text = enText,
                        ApprovalStatus = "Approved"
                    };
                    _db.Narrations.Add(enNarration);
                    await _db.SaveChangesAsync(); // Save to generate ID
                    
                    enNarration.FileUrl = await _audioService.GenerateTtsAsync(enText, "en", enNarration.Id, _env.WebRootPath);
                    enNarration.AudioStatus = "ready";

                    // Generate Japanese Translation and Audio
                    var jaText = await _audioService.TranslateAsync(narration.Text, "ja");
                    var jaNarration = new TravelSystem.Shared.Models.Narration
                    {
                        ZoneId = narration.ZoneId,
                        Language = "ja",
                        Text = jaText,
                        ApprovalStatus = "Approved"
                    };
                    _db.Narrations.Add(jaNarration);
                    await _db.SaveChangesAsync(); // Save to generate ID
                    
                    jaNarration.FileUrl = await _audioService.GenerateTtsAsync(jaText, "ja", jaNarration.Id, _env.WebRootPath);
                    jaNarration.AudioStatus = "ready";
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
    }
}
