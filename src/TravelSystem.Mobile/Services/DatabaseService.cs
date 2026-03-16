using SQLite;
using TravelSystem.Shared.Models;
using TravelSystem.Shared.Factories;

namespace TravelSystem.Mobile.Services;

public class DatabaseService
{
    private readonly SQLiteAsyncConnection _connection;
    private readonly string _dbPath;

    // ⚠️ CHỈ DÙNG KHI DEV: đặt true để xóa db cũ & tạo lại schema mới
    // Đổi lại false sau khi chạy 1 lần để tránh mất data
    private const bool DEV_RESET_DATABASE = false;

    public DatabaseService(SqliteConnectionFactory factory)
    {
        _dbPath = Path.Combine(FileSystem.AppDataDirectory, "travelsystem_offline.db3");
        _connection = factory.CreateConnection(_dbPath);
    }

    private bool _isInitialized = false;

    public async Task InitializeAsync()
    {
        if (_isInitialized) return;

        try
        {
#if DEBUG
            // Xóa file db cũ nếu đang dev và cần reset schema
            if (DEV_RESET_DATABASE && File.Exists(_dbPath))
            {
                await _connection.CloseAsync();
                File.Delete(_dbPath);
                Console.WriteLine("🗑️ Database cũ đã bị xóa — tạo lại schema mới...");
            }
#endif
            await _connection.CreateTablesAsync<
                LocalRoute,
                LocalZone,
                LocalNarration,
                LocalAnalytics,
                AppSetting>();

            var localRouteColumns = await _connection.GetTableInfoAsync(nameof(LocalRoute));
            if (!localRouteColumns.Any(c => c.Name.Equals(nameof(LocalRoute.ImageUrl), StringComparison.OrdinalIgnoreCase)))
            {
                await _connection.ExecuteAsync($"ALTER TABLE {nameof(LocalRoute)} ADD COLUMN {nameof(LocalRoute.ImageUrl)} TEXT");
            }

            _isInitialized = true;
            Console.WriteLine("✅ Bùi Viện Database Initialized!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Database init failed: {ex.Message}");
        }
    }

    // LẤY CÀI ĐẶT
    public async Task<string> GetSettingAsync(string key, string defaultValue = "")
    {
        await InitializeAsync();
        var setting = await _connection.Table<AppSetting>().FirstOrDefaultAsync(s => s.Key == key);
        return setting?.Value ?? defaultValue;
    }

    // LƯU CÀI ĐẶT
    public async Task SetSettingAsync(string key, string value)
    {
        await InitializeAsync();
        var setting = await _connection.Table<AppSetting>().FirstOrDefaultAsync(s => s.Key == key);
        if (setting == null)
        {
            await _connection.InsertAsync(new AppSetting { Key = key, Value = value, UpdatedAt = DateTime.UtcNow });
        }
        else
        {
            setting.Value = value;
            setting.UpdatedAt = DateTime.UtcNow;
            await _connection.UpdateAsync(setting);
        }
    }

    public async Task SaveRouteSyncDataAsync(RouteSyncData syncData)
    {
        await InitializeAsync();

        var syncedAt = syncData.Timestamp.ToString("o");
        var localRoutes = syncData.Routes.Select(route => new LocalRoute
        {
            Id = route.Id,
            RouteId = route.Id.ToString(),
            Name = route.Name ?? string.Empty,
            Description = route.Description ?? string.Empty,
            ImageUrl = route.ImageUrl ?? string.Empty,
            StartLatitude = (double)route.StartLatitude,
            StartLongitude = (double)route.StartLongitude,
            QRCode = string.Empty,
            SyncedAt = syncedAt,
            IsActive = route.IsActive ? 1 : 0
        }).ToList();

        var localZones = syncData.Zones.Select(zone => new LocalZone
        {
            Id = zone.Id,
            ZoneId = zone.Id.ToString(),
            RouteId = zone.RouteId.ToString(),
            Name = zone.Name ?? string.Empty,
            Description = zone.Description ?? string.Empty,
            ImageUrl = string.Empty,
            Latitude = (double)zone.Latitude,
            Longitude = (double)zone.Longitude,
            Radius = zone.Radius,
            OrderIndex = zone.OrderIndex,
            ZoneType = zone.ZoneType,
            IsActive = zone.IsActive ? 1 : 0,
            ActiveTime = 0,
            UpdatedAt = zone.UpdatedAt.ToString("o")
        }).ToList();

        var localNarrations = syncData.Narrations.Select(narration => new LocalNarration
        {
            Id = narration.Id,
            ZoneId = narration.ZoneId.ToString(),
            Language = narration.Language ?? string.Empty,
            Text = narration.Text ?? string.Empty,
            VoiceId = narration.VoiceId ?? string.Empty,
            FileUrl = string.Empty,
            LocalFilePath = string.Empty,
            Version = 1,
            SyncedAt = syncedAt
        }).ToList();

        await _connection.RunInTransactionAsync(connection =>
        {
            connection.DeleteAll<LocalNarration>();
            connection.DeleteAll<LocalZone>();
            connection.DeleteAll<LocalRoute>();

            connection.InsertAll(localRoutes);
            connection.InsertAll(localZones);
            connection.InsertAll(localNarrations);
        });

        await SetSettingAsync("LastSyncedAt", syncedAt);
    }

    public async Task<IReadOnlyList<RouteSummary>> GetRouteSummariesAsync()
    {
        await InitializeAsync();

        var routes = await _connection.Table<LocalRoute>()
            .Where(r => r.IsActive == 1)
            .OrderBy(r => r.Id)
            .ToListAsync();

        var zones = await _connection.Table<LocalZone>()
            .Where(z => z.IsActive == 1)
            .ToListAsync();

        var result = new List<RouteSummary>(routes.Count);
        foreach (var route in routes)
        {
            var routeIdText = string.IsNullOrWhiteSpace(route.RouteId) ? route.Id.ToString() : route.RouteId;
            var routeZones = zones.Where(z => z.RouteId == routeIdText).ToList();

            var stopCount = routeZones.Count;
            var minutes = routeZones.Sum(z => z.ActiveTime > 0 ? z.ActiveTime : 0);
            if (minutes <= 0)
            {
                minutes = stopCount * 8;
            }

            var zoneType = routeZones.FirstOrDefault()?.ZoneType ?? 0;
            var routeId = int.TryParse(routeIdText, out var parsedRouteId) ? parsedRouteId : route.Id;

            result.Add(new RouteSummary(
                routeId,
                route.Name,
                route.Description,
                stopCount,
                minutes,
                zoneType,
                NormalizeImageUrl(route.ImageUrl)));
        }

        return result;
    }

    private static string NormalizeImageUrl(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return string.Empty;
        }

        imageUrl = imageUrl.Trim().Replace('\\', '/');

        if (Uri.TryCreate(imageUrl, UriKind.Absolute, out var absoluteUri))
        {
            return absoluteUri.ToString();
        }

        var relativePath = imageUrl.TrimStart('~', '/');
        var baseUrl = ApiConstants.BaseApiUrl.EndsWith('/')
            ? ApiConstants.BaseApiUrl
            : $"{ApiConstants.BaseApiUrl}/";

        return new Uri(new Uri(baseUrl), relativePath).ToString();
    }
}