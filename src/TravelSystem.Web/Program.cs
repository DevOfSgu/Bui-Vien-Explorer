using Microsoft.EntityFrameworkCore;
using TravelSystem.Web.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Add DbContext
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Authentication Configuration
builder.Services.AddAuthentication()
    // 1. Admin Auth Scheme
    .AddCookie("AdminAuth", options =>
    {
        options.LoginPath = "/Admin/Auth/Login";
        options.LogoutPath = "/Admin/Auth/Logout";
        options.AccessDeniedPath = "/Admin/Auth/AccessDenied";
        options.Cookie.Name = "AdminAuthCookie";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
    })
    // 2. Vendor Auth Scheme
    .AddCookie("VendorAuth", options =>
    {
        options.LoginPath = "/Vendor/Auth/Login";
        options.LogoutPath = "/Vendor/Auth/Logout";
        options.AccessDeniedPath = "/Vendor/Auth/AccessDenied";
        options.Cookie.Name = "VendorAuthCookie";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
    });
var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Ensure database schema contains expected columns (ImageUrl for Routes; no QRCode/IsOpen columns anymore).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.ExecuteSqlRaw("IF COL_LENGTH('Routes','ImageUrl') IS NULL ALTER TABLE Routes ADD ImageUrl nvarchar(max) NULL;");
    db.Database.ExecuteSqlRaw("IF COL_LENGTH('Routes','QRCode') IS NOT NULL ALTER TABLE Routes DROP COLUMN QRCode;");
    db.Database.ExecuteSqlRaw("IF COL_LENGTH('ShopHours','IsOpen') IS NOT NULL ALTER TABLE ShopHours DROP COLUMN IsOpen;");
}

app.MapStaticAssets();

// Route for Areas (Admin & Vendor)
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
