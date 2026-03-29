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
        private readonly IWebHostEnvironment _env;

        public ToursController(AppDbContext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
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
                var selectedIds = selectedZoneIds?.ToList() ?? new List<int>();

                // 1. Xóa các điểm không còn trong danh sách chọn
                foreach (var tz in currentTourZones)
                {
                    if (!selectedIds.Contains(tz.ZoneId))
                    {
                        dbTour.TourZones.Remove(tz);
                    }
                }

                // 2. Thêm hoặc cập nhật (thứ tự) các điểm đã chọn
                if (selectedZoneIds != null)
                {
                    for (int i = 0; i < selectedZoneIds.Length; i++)
                    {
                        var zoneId = selectedZoneIds[i];
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
                                OrderIndex = i + 1
                            });
                        }
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
    }
}
