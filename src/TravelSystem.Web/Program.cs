using Microsoft.EntityFrameworkCore;
using TravelSystem.Web.Data;

// Load environment variables from .env file
DotNetEnv.Env.Load();

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient<TravelSystem.Web.Services.IAudioTranslationService, TravelSystem.Web.Services.FreeAudioTranslationService>();
builder.Services.AddScoped<TravelSystem.Web.Services.INotificationService, TravelSystem.Web.Services.NotificationService>();

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
// 1. Initial Database Schema Patches (Direct SQL)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (!string.IsNullOrEmpty(connectionString))
{
    using var connection = new Microsoft.Data.SqlClient.SqlConnection(connectionString);
    try
    {
        connection.Open();
        var sql = @"
            IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Zones')
            BEGIN
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Zones') AND name = 'IsMain')
                BEGIN
                    ALTER TABLE Zones ADD IsMain BIT NOT NULL DEFAULT 0;
                END
            END";
        using var command = new Microsoft.Data.SqlClient.SqlCommand(sql, connection);
        command.ExecuteNonQuery();
        Console.WriteLine("Database Patch: Zones.IsMain verified/added.");

        var notificationSql = @"
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AppNotifications')
            BEGIN
                CREATE TABLE AppNotifications(
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    RecipientUserId INT NULL,
                    RecipientRole NVARCHAR(20) NOT NULL,
                    Message NVARCHAR(500) NOT NULL,
                    LinkUrl NVARCHAR(500) NULL,
                    IsRead BIT NOT NULL CONSTRAINT DF_AppNotifications_IsRead DEFAULT(0),
                    CreatedAt DATETIME2 NOT NULL,
                    ReadAt DATETIME2 NULL
                );
            END;

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AppNotifications_RecipientRole_RecipientUserId_IsRead_CreatedAt' AND object_id = OBJECT_ID('AppNotifications'))
            BEGIN
                CREATE INDEX IX_AppNotifications_RecipientRole_RecipientUserId_IsRead_CreatedAt
                ON AppNotifications (RecipientRole, RecipientUserId, IsRead, CreatedAt);
            END;
        ";
        using var notificationCommand = new Microsoft.Data.SqlClient.SqlCommand(notificationSql, connection);
        notificationCommand.ExecuteNonQuery();
        Console.WriteLine("Database Patch: AppNotifications verified/created.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Database Patch Error: {ex.Message}");
    }
}

var app = builder.Build();

// Schema patches intentionally removed: Shops no longer include Description/Radius

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.UseStaticFiles();
app.MapStaticAssets();
app.MapControllers();

// Route for Areas (Admin & Vendor)
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
