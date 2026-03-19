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
        _dbPath = Path.Combine(FileSystem.AppDataDirectory, "BuiVienExplorer.db3");
        _connection = factory.CreateConnection(_dbPath);
    }

    private bool _isInitialized = false;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public async Task InitializeAsync()
    {
        if (_isInitialized) return;

        await _initLock.WaitAsync();

        try
        {
            if (_isInitialized) return;

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
                LocalZone,
                LocalNarration,
                LocalAnalytics,
                AppSetting>();

            var localZoneColumns = await _connection.GetTableInfoAsync(nameof(LocalZone));
            if (!localZoneColumns.Any(c => c.Name.Equals(nameof(LocalZone.ImageUrl), StringComparison.OrdinalIgnoreCase)))
            {
                await _connection.ExecuteAsync($"ALTER TABLE {nameof(LocalZone)} ADD COLUMN {nameof(LocalZone.ImageUrl)} TEXT");
            }

            _isInitialized = true;
            Console.WriteLine("✅ Bùi Viện Database Initialized!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Database init failed: {ex.Message}");
        }
        finally
        {
            _initLock.Release();
        }
    }

    // LẤY CÀI ĐẶT
    public async Task<string> GetSettingAsync(string key, string defaultValue = "")
    {
        try
        {
            await InitializeAsync();
            var setting = await _connection.Table<AppSetting>().FirstOrDefaultAsync(s => s.Key == key);
            return setting?.Value ?? defaultValue;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ GetSettingAsync failed ({key}): {ex.Message}");
            return defaultValue;
        }
    }

    // LƯU CÀI ĐẶT
    public async Task SetSettingAsync(string key, string value)
    {
        try
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
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ SetSettingAsync failed ({key}): {ex.Message}");
        }
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
            if (absoluteUri.Scheme == Uri.UriSchemeFile)
            {
                var filePath = absoluteUri.LocalPath.Replace('\\', '/').TrimStart('/');
                var normalizedBaseUrl = ApiConstants.GetBaseApiUrl();

                return new Uri(new Uri(normalizedBaseUrl), filePath).ToString();
            }

            if (absoluteUri.Scheme != Uri.UriSchemeHttp && absoluteUri.Scheme != Uri.UriSchemeHttps)
            {
                return string.Empty;
            }

            return absoluteUri.ToString();
        }

        var relativePath = imageUrl.TrimStart('~', '/');
        var baseUrl = ApiConstants.GetBaseApiUrl();

        return new Uri(new Uri(baseUrl), relativePath).ToString();
    }
}