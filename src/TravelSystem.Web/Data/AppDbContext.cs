// TravelSystem.Web/Data/AppDbContext.cs

using Microsoft.EntityFrameworkCore;
using TravelSystem.Shared.Models;

namespace TravelSystem.Web.Data;

public class AppDbContext : DbContext
{
    public DbSet<Routes> Routes { get; set; }
    public DbSet<Zone> Zones { get; set; }
    
    public AppDbContext(DbContextOptions<AppDbContext> options) 
        : base(options) { }
}