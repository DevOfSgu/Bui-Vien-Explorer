using System.Diagnostics;
using TravelSystem.Shared.Models;

namespace TravelSystem.Mobile.Services;

public class AudioPreloadService
{
    private readonly ApiService _apiService;
    private readonly DatabaseService _dbService;
    private readonly HttpClient _httpClient;
    private bool _isSyncing = false;

    public AudioPreloadService(ApiService apiService, DatabaseService dbService)
    {
        _apiService = apiService;
        _dbService = dbService;
        _httpClient = new HttpClient();
    }

    /// <summary>
    /// Bắt đầu tiến trình đồng bộ và tải xuống các file audio còn thiếu
    /// </summary>
    public async Task StartPreloadAsync()
    {
        if (_isSyncing) return;
        _isSyncing = true;

        try
        {
            Debug.WriteLine("[DEBUG][PRELOAD] Starting Audio Preload Process...");
            
            // 1. Lấy danh sách narration cần tải
            var pending = await _dbService.GetPendingDownloadsAsync();
            if (pending.Count == 0)
            {
                Debug.WriteLine("[DEBUG][PRELOAD] No pending downloads.");
                return;
            }

            Debug.WriteLine($"[DEBUG][PRELOAD] Found {pending.Count} pending audio files.");

            // tạo thư mục lưu trữ nếu chưa có
            string audioDir = Path.Combine(FileSystem.AppDataDirectory, "audio_cache");
            if (!Directory.Exists(audioDir))
                Directory.CreateDirectory(audioDir);

            foreach (var item in pending)
            {
                try
                {
                    // 2. Đánh dấu đang tải
                    await _dbService.UpdateDownloadStatusAsync(item.Id, 1);

                    // 3. Tải file
                    string fileName = $"{item.ZoneId}_{item.Language}.mp3";
                    string localPath = Path.Combine(audioDir, fileName);
                    
                    Debug.WriteLine($"[DEBUG][PRELOAD] Downloading: {item.FileUrl}");

                    // Lấy BaseUrl từ ApiService nếu FileUrl là đường dẫn tương đối
                    var downloadUrl = item.FileUrl;
                    if (!downloadUrl.StartsWith("http"))
                    {
                        var baseUrl = ApiConstants.GetBaseApiUrl().TrimEnd('/');
                        downloadUrl = $"{baseUrl}/{downloadUrl.TrimStart('/')}";
                    }

                    var response = await _httpClient.GetAsync(downloadUrl);
                    if (response.IsSuccessStatusCode)
                    {
                        using var fs = new FileStream(localPath, FileMode.Create);
                        await response.Content.CopyToAsync(fs);
                        
                        // 4. Cập nhật thành công
                        await _dbService.UpdateDownloadStatusAsync(item.Id, 2, localPath);
                        Debug.WriteLine($"[DEBUG][PRELOAD] Successfully downloaded to: {localPath}");
                    }
                    else
                    {
                        await _dbService.UpdateDownloadStatusAsync(item.Id, 3);
                        Debug.WriteLine($"[DEBUG][PRELOAD] Download failed with status: {response.StatusCode}");
                    }
                }
                catch (Exception ex)
                {
                    await _dbService.UpdateDownloadStatusAsync(item.Id, 3);
                    Debug.WriteLine($"[DEBUG][PRELOAD] Error downloading {item.FileUrl}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DEBUG][PRELOAD] Preload process critical error: {ex.Message}");
        }
        finally
        {
            _isSyncing = false;
            Debug.WriteLine("[DEBUG][PRELOAD] Preload process finished.");
        }
    }
}
