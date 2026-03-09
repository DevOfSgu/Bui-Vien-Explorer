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

        public NarrationsController(AppDbContext db)
        {
            _db = db;
        }

        public async Task<IActionResult> Index(int? zoneId)
        {
            var query = _db.Narrations.AsQueryable();

            if (zoneId.HasValue)
            {
                query = query.Where(n => n.ZoneId == zoneId.Value);
                ViewBag.SelectedZoneId = zoneId;
                ViewBag.ZoneName = _db.Zones.Find(zoneId.Value)?.Name;
            }

            var narrations = await query.OrderBy(n => n.Id).ToListAsync();
            
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
            if (narration != null)
            {
                narration.ApprovalStatus = "Approved";
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
