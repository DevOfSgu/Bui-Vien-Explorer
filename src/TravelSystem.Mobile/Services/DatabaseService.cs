using SQLite;
using TravelSystem.Shared.Models;
using TravelSystem.Shared.Factories;
using System.Diagnostics;

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
            // Create core tables (CreateTablesAsync supports up to 4 generic type params)
            await _connection.CreateTablesAsync<
                LocalZone,
                LocalNarration,
                LocalAnalytics,
                AppSetting>();

            // Create additional tables individually to avoid exceeding generic parameter limits
            await _connection.CreateTableAsync<LocalFavorite>();
            await _connection.CreateTableAsync<PendingFavoriteOp>();

            var localZoneColumns = await _connection.GetTableInfoAsync(nameof(LocalZone));
            if (!localZoneColumns.Any(c => c.Name.Equals(nameof(LocalZone.ImageUrl), StringComparison.OrdinalIgnoreCase)))
            {
                await _connection.ExecuteAsync($"ALTER TABLE {nameof(LocalZone)} ADD COLUMN {nameof(LocalZone.ImageUrl)} TEXT");
            }

            // Ensure LocalNarration table has expected columns (migration path)
            var narrationColumns = await _connection.GetTableInfoAsync(nameof(LocalNarration));
            if (!narrationColumns.Any(c => c.Name.Equals(nameof(LocalNarration.ContentHash), StringComparison.OrdinalIgnoreCase)))
            {
                await _connection.ExecuteAsync($"ALTER TABLE {nameof(LocalNarration)} ADD COLUMN {nameof(LocalNarration.ContentHash)} TEXT");
            }
            if (!narrationColumns.Any(c => c.Name.Equals(nameof(LocalNarration.DownloadStatus), StringComparison.OrdinalIgnoreCase)))
            {
                await _connection.ExecuteAsync($"ALTER TABLE {nameof(LocalNarration)} ADD COLUMN {nameof(LocalNarration.DownloadStatus)} INTEGER DEFAULT 0");
            }

            // Ensure LocalFavorite table has expected columns (migration path)
            var localFavColumns = await _connection.GetTableInfoAsync(nameof(TravelSystem.Shared.Models.LocalFavorite));
            // No schema changes needed now, placeholder for future migrations

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

    // Local favorites helpers
    public async Task<List<LocalFavorite>> GetLocalFavoritesAsync(string guestId)
    {
        try
        {
            await InitializeAsync();
            return await _connection.Table<LocalFavorite>().Where(l => l.GuestId == guestId && l.IsDeleted == 0).ToListAsync();
        }
        catch
        {
            return new List<LocalFavorite>();
        }
    }

    public async Task InsertOrUpdateLocalFavoriteAsync(LocalFavorite fav)
    {
        try
        {
            await InitializeAsync();
            var existing = await _connection.Table<LocalFavorite>().FirstOrDefaultAsync(l => l.GuestId == fav.GuestId && l.ZoneId == fav.ZoneId);
            if (existing == null)
            {
                await _connection.InsertAsync(fav);
            }
            else
            {
                existing.IsDeleted = fav.IsDeleted;
                existing.UpdatedAt = DateTime.UtcNow;
                existing.CreatedAt = fav.CreatedAt;
                await _connection.UpdateAsync(existing);
            }
        }
        catch
        {
        }
    }

    public async Task MarkLocalFavoriteDeletedAsync(string guestId, int zoneId)
    {
        try
        {
            await InitializeAsync();
            var existing = await _connection.Table<LocalFavorite>().FirstOrDefaultAsync(l => l.GuestId == guestId && l.ZoneId == zoneId);
            if (existing != null)
            {
                existing.IsDeleted = 1;
                existing.UpdatedAt = DateTime.UtcNow;
                await _connection.UpdateAsync(existing);
            }
        }
        catch
        {
        }
    }

    public async Task InsertPendingOpAsync(PendingFavoriteOp op)
    {
        try
        {
            await InitializeAsync();
            await _connection.InsertAsync(op);
        }
        catch
        {
        }
    }

    public async Task<List<PendingFavoriteOp>> GetPendingOpsAsync()
    {
        try
        {
            await InitializeAsync();
            return await _connection.Table<PendingFavoriteOp>().Where(p => p.Processed == 0).OrderBy(p => p.CreatedAt).ToListAsync();
        }
        catch
        {
            return new List<PendingFavoriteOp>();
        }
    }

    public async Task UpdatePendingOpAsync(PendingFavoriteOp op)
    {
        try
        {
            await InitializeAsync();
            await _connection.UpdateAsync(op);
        }
        catch
        {
        }
    }

    public async Task ReplaceLocalFavoritesForGuestAsync(string guestId, List<LocalFavorite> favorites)
    {
        try
        {
            await InitializeAsync();
            await _connection.RunInTransactionAsync(tran =>
            {
                tran.Execute($"DELETE FROM {nameof(LocalFavorite)} WHERE GuestId = ?", guestId);
                foreach (var f in favorites)
                {
                    tran.Insert(f);
                }
            });
        }
        catch
        {
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

    // LẤY THUYẾT MINH (NARRATION)
    public async Task<LocalNarration?> GetNarrationAsync(int zoneId, string language)
    {
        try
        {
            await InitializeAsync();
            // ZoneId trong LocalNarration là string, cần convert hoặc so khớp
            var zoneIdStr = zoneId.ToString().Trim();
            var langNorm = language?.Trim().ToLower() ?? "vi";
            
            Debug.WriteLine($"[DEBUG][DB] Querying narration for ZoneId: '{zoneIdStr}', Language: '{langNorm}'");
            
            var result = await _connection.Table<LocalNarration>()
                .FirstOrDefaultAsync(n => n.ZoneId == zoneIdStr && n.Language == langNorm);

            if (result != null)
                Debug.WriteLine($"[DEBUG][DB] Narration found. Text length: {result.Text?.Length ?? 0}");
            else
            {
                Debug.WriteLine("[DEBUG][DB] Narration NOT found in local database.");
                // Log all available ZoneIds for the language to help troubleshooting
                var allForLang = await _connection.Table<LocalNarration>().Where(x => x.Language == langNorm).ToListAsync();
                Debug.WriteLine($"[DEBUG][DB] Total narrations for lang '{langNorm}': {allForLang.Count}. ZoneIds: {string.Join(", ", allForLang.Select(x => x.ZoneId))}");
            }

            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ GetNarrationAsync failed (ZoneId={zoneId}, Lang={language}): {ex.Message}");
            return null;
        }
    }
    
    // UPSERT DANH SÁCH THUYẾT MINH
    public async Task UpsertNarrationsAsync(IEnumerable<LocalNarration> narrations)
    {
        try
        {
            await InitializeAsync();
            await _connection.RunInTransactionAsync(tran =>
            {
                foreach (var n in narrations)
                {
                    // Chuẩn hóa dữ liệu trước khi lưu
                    n.ZoneId = n.ZoneId?.Trim() ?? string.Empty;
                    n.Language = n.Language?.Trim().ToLower() ?? string.Empty;

                    // Kiểm tra xem đã có bản ghi cho ZoneId + Lang chưa
                    var existing = tran.Table<LocalNarration>()
                        .FirstOrDefault(x => x.ZoneId == n.ZoneId && x.Language == n.Language);


                    if (existing == null)
                    {
                        // New record
                        if (!string.IsNullOrEmpty(n.Text))
                        {
                            n.ContentHash = GenerateHash(n.Text);
                        }
                        tran.Insert(n);
                    }
                    else
                    {
                        // Update existing
                        var newHash = !string.IsNullOrEmpty(n.Text) ? GenerateHash(n.Text) : string.Empty;
                        
                        // Nếu hash thay đổi hoặc FileUrl thay đổi -> Reset trạng thái download
                        if (existing.ContentHash != newHash || existing.FileUrl != n.FileUrl)
                        {
                            existing.DownloadStatus = 0; // NotStarted
                            existing.LocalFilePath = string.Empty; // Invalidate old file
                        }

                        existing.Text = n.Text;
                        existing.FileUrl = n.FileUrl;
                        existing.ContentHash = newHash;
                        existing.Language = n.Language;
                        existing.UpdatedAt = DateTime.UtcNow;
                        tran.Update(existing);
                    }
                }
            });
            Console.WriteLine($"✅ Đã cập nhật {narrations.Count()} bản ghi thuyết minh.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ UpsertNarrationsAsync failed: {ex.Message}");
        }
    }

    private static string GenerateHash(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        // Simple fast hash for comparison
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(text);
        var hashBytes = sha.ComputeHash(bytes);
        return Convert.ToBase64String(hashBytes);
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
    // Xóa tất cả PendingOp đã processed
    public async Task CleanupProcessedOpsAsync()
    {
        await InitializeAsync();
        await _connection.ExecuteAsync(
            "DELETE FROM PendingFavoriteOp WHERE Processed = 1");
    }

    // Xóa PendingOp trùng (giữ lại op mới nhất cho mỗi GuestId+ZoneId+Operation)
    public async Task DeduplicatePendingOpsAsync()
    {
        await InitializeAsync();
        await _connection.ExecuteAsync(@"
        DELETE FROM PendingFavoriteOp 
        WHERE Id NOT IN (
            SELECT MAX(Id) FROM PendingFavoriteOp 
            WHERE Processed = 0
            GROUP BY GuestId, ZoneId, Operation
        ) AND Processed = 0");
    }

    // LẤY DANH SÁCH CẦN TẢI AUDIO
    public async Task<List<LocalNarration>> GetPendingDownloadsAsync()
    {
        try
        {
            await InitializeAsync();
            return await _connection.Table<LocalNarration>()
                .Where(n => n.DownloadStatus != 2 && !string.IsNullOrEmpty(n.FileUrl))
                .ToListAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"⚠️ GetPendingDownloadsAsync failed: {ex.Message}");
            return new List<LocalNarration>();
        }
    }

    // CẬP NHẬT TRẠNG THÁI TẢI
    public async Task UpdateDownloadStatusAsync(int id, int status, string localPath = "")
    {
        try
        {
            await InitializeAsync();
            var existing = await _connection.Table<LocalNarration>().FirstOrDefaultAsync(n => n.Id == id);
            if (existing != null)
            {
                existing.DownloadStatus = status;
                if (!string.IsNullOrEmpty(localPath))
                {
                    existing.LocalFilePath = localPath;
                }
                existing.UpdatedAt = DateTime.UtcNow;
                await _connection.UpdateAsync(existing);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"⚠️ UpdateDownloadStatusAsync failed: {ex.Message}");
        }
    }
}