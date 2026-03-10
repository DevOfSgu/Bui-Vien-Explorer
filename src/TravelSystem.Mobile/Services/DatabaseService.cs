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
}