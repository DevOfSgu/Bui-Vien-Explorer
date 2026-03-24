using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;
using System.Diagnostics;

namespace TravelSystem.Mobile.Services;

public static class MapHelper
{
    // Tên file bản đồ duy nhất (người dùng sẽ tự chuẩn bị file này chứa đủ các zoom level)
    public const string OfflineMapFileName = "hcm_map.mbtiles";

    public static string GetOfflineMapPath()
    {
        return Path.Combine(FileSystem.AppDataDirectory, OfflineMapFileName);
    }

    // Biến static để lưu trạng thái task
    private static Task? _initializationTask;
    private static readonly object _lock = new();

    public static Task EnsureOfflineMapExistsAsync()
    {
        lock (_lock)
        {
            if (_initializationTask == null || _initializationTask.IsFaulted)
            {
                _initializationTask = RunEnsureOfflineMapExistsAsync();
            }
            return _initializationTask;
        }
    }

    private static async Task RunEnsureOfflineMapExistsAsync()
    {
        string targetPath = GetOfflineMapPath();
        
        if (File.Exists(targetPath))
        {
            var info = new FileInfo(targetPath);
            if (info.Length > 1024 * 1024) 
            {
                Debug.WriteLine($"[MAP] File {OfflineMapFileName} đã có sẵn ({info.Length} bytes).");
                return;
            }
            try { File.Delete(targetPath); } catch { }
        }

        try
        {
            Debug.WriteLine($"[MAP] Đang giải nén {OfflineMapFileName}...");
            using var stream = await FileSystem.OpenAppPackageFileAsync(OfflineMapFileName);
            using var fileStream = File.Create(targetPath);
            await stream.CopyToAsync(fileStream);
            Debug.WriteLine($"[MAP] ✅ Giải nén {OfflineMapFileName} thành công.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MAP] ❌ Lỗi khi copy {OfflineMapFileName}: {ex.Message}");
            throw;
        }
    }
}
