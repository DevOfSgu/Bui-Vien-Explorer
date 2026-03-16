using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TravelSystem.Shared.Models;
using TravelSystem.Web.Data;

namespace TravelSystem.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(AuthenticationSchemes = "AdminAuth", Roles = "Admin")]
    public class ToursController : Controller
    {
        private readonly AppDbContext _db;

        public ToursController(AppDbContext db)
        {
            _db = db;
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
            ViewBag.Zones = await _db.Zones.Where(z => z.IsActive).OrderBy(z => z.Name).ToListAsync();
            return View(new Tour());
        }

        // POST: Admin/Tours/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Tour tour, int[] selectedZoneIds)
        {
            if (ModelState.IsValid)
            {
                tour.CreatedAt = DateTime.UtcNow;
                tour.UpdatedAt = DateTime.UtcNow;
                _db.Tours.Add(tour);
                await _db.SaveChangesAsync(); // Get Tour Id

                if (selectedZoneIds != null)
                {
                    for (int i = 0; i < selectedZoneIds.Length; i++)
                    {
                        _db.TourZones.Add(new TourZone
                        {
                            TourId = tour.Id,
                            ZoneId = selectedZoneIds[i],
                            OrderIndex = i + 1
                        });
                    }
                    await _db.SaveChangesAsync();
                }

                return RedirectToAction(nameof(Index));
            }

            ViewBag.Zones = await _db.Zones.Where(z => z.IsActive).OrderBy(z => z.Name).ToListAsync();
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

            ViewBag.Zones = await _db.Zones.Where(z => z.IsActive).OrderBy(z => z.Name).ToListAsync();
            return View(tour);
        }

        // POST: Admin/Tours/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Tour tour, int[] selectedZoneIds)
        {
            if (id != tour.Id) return NotFound();

            if (ModelState.IsValid)
            {
                var dbTour = await _db.Tours.FindAsync(id);
                if (dbTour == null) return NotFound();

                dbTour.Name = tour.Name;
                dbTour.Description = tour.Description;
                dbTour.ImageUrl = tour.ImageUrl;
                dbTour.Duration = tour.Duration;
                dbTour.UpdatedAt = DateTime.UtcNow;

                // Update Zones
                var existingZones = _db.TourZones.Where(tz => tz.TourId == id);
                _db.TourZones.RemoveRange(existingZones);

                if (selectedZoneIds != null)
                {
                    for (int i = 0; i < selectedZoneIds.Length; i++)
                    {
                        _db.TourZones.Add(new TourZone
                        {
                            TourId = tour.Id,
                            ZoneId = selectedZoneIds[i],
                            OrderIndex = i + 1
                        });
                    }
                }

                await _db.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Zones = await _db.Zones.Where(z => z.IsActive).OrderBy(z => z.Name).ToListAsync();
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
                _db.Tours.Remove(tour);
                await _db.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
