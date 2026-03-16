using Microsoft.EntityFrameworkCore;
using TravelSystem.Shared.Models;

namespace TravelSystem.Web.Data;

public class AppDbContext : DbContext
{
    public DbSet<Zone> Zones { get; set; }
    public DbSet<Narration> Narrations { get; set; }
    public DbSet<Shop> Shops { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<Analytics> Analytics { get; set; }
    public DbSet<GuestFavorite> GuestFavorites { get; set; }
    public DbSet<ShopHour> ShopHours { get; set; }
    public DbSet<Tour> Tours { get; set; }
    public DbSet<TourZone> TourZones { get; set; }


    // store global configuration items keyed by string
    public DbSet<TravelSystem.Shared.Models.AppSetting> AppSettings { get; set; }

    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Narration → Zone (1 zone có nhiều narrations - đa ngôn ngữ)
        modelBuilder.Entity<Narration>()
            .HasOne<Zone>()
            .WithMany()
            .HasForeignKey(n => n.ZoneId)
            .OnDelete(DeleteBehavior.Cascade);

        // AppSetting uses string key
        modelBuilder.Entity<TravelSystem.Shared.Models.AppSetting>()
            .HasKey(s => s.Key);

        // TourZone N-N (Composite Key)
        modelBuilder.Entity<TourZone>()
            .HasKey(tz => new { tz.TourId, tz.ZoneId });

        modelBuilder.Entity<TourZone>()
            .HasOne(tz => tz.Tour)
            .WithMany(t => t.TourZones)
            .HasForeignKey(tz => tz.TourId);

        modelBuilder.Entity<TourZone>()
            .HasOne(tz => tz.Zone)
            .WithMany()
            .HasForeignKey(tz => tz.ZoneId);

        // Configure Decimal Precision for Maps
        modelBuilder.Entity<Zone>()
            .Property(z => z.Latitude).HasPrecision(18, 15);
        modelBuilder.Entity<Zone>()
            .Property(z => z.Longitude).HasPrecision(18, 15);

        modelBuilder.Entity<Analytics>()
            .Property(a => a.Latitude).HasPrecision(18, 15);
        modelBuilder.Entity<Analytics>()
            .Property(a => a.Longitude).HasPrecision(18, 15);
    }

}