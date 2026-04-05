using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Linq;
using System.Net.Http.Headers;
using System.Globalization;
using System.IO;
using TravelSystem.Shared.Models;

namespace TravelSystem.Mobile.Services;

public class ApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ApiService> _logger;
    private readonly DatabaseService _dbService;
    private readonly SemaphoreSlim _guestIdLock = new(1, 1);
    private readonly SemaphoreSlim _analyticsSessionLock = new(1, 1);
    private readonly SemaphoreSlim _installSyncLock = new(1, 1);
    private readonly SemaphoreSlim _analyticsSyncLock = new(1, 1);
    private readonly ConcurrentDictionary<int, IReadOnlyList<TourStopDto>> _tourStopsCache = new();
    private IReadOnlyList<TourSummaryDto>? _toursMemoryCache;
    private static readonly TimeSpan TourStopsRequestTimeout = TimeSpan.FromSeconds(3);
    private static readonly JsonSerializerOptions CacheJsonOptions = new() { PropertyNameCaseInsensitive = true };
    private const string ToursCacheKey = "cache_tours_v1";
    private static readonly HashSet<string> AllowedAnalyticsActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "AppInstall",
        "EnterZone",
        "PlayNarration",
        "LocationPing"
    };

    public ApiService(ILogger<ApiService> logger, DatabaseService dbService)
    {
        _logger = logger;
        _dbService = dbService;
#if DEBUG && ANDROID
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true
        };
        _httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        _logger.LogWarning("[API][DEBUG] SSL certificate validation is disabled on Android DEBUG build.");
#else
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30) // Increased for initial sync
        };
#endif
        // Prevent ngrok browser warning page from being returned to API calls.
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("ngrok-skip-browser-warning", "true");
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _logger.LogInformation("[API] Initialized with BaseUrl: {BaseUrl}", ApiConstants.GetBaseApiUrl());
    }

    // Simple sync: push pending ops then pull latest favorites into local table
    public async Task SyncFavoritesIfOnlineAsync(CancellationToken cancellationToken = default)
    {

        try
        {
            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
            {
                _logger.LogInformation("[API][SYNC] Favorites sync skipped: offline");
                return;
            }

            await _dbService.InitializeAsync();
            await _dbService.CleanupProcessedOpsAsync();    // ← dọn op cũ
            await _dbService.DeduplicatePendingOpsAsync(); // ← dedup op trùng

            // Push pending ops
            var pending = await _dbService.GetPendingOpsAsync();
            foreach (var op in pending)
            {
                _logger.LogInformation("[FAV][SYNC] Processing op Id={Id} Op={Op} ZoneId={ZoneId} Attempts={Attempts}",
                op.Id, op.Operation, op.ZoneId, op.AttemptCount);
                try
                {
                    bool ok = false;
                    if (op.Operation == TravelSystem.Shared.Models.FavoriteOperation.Add)
                    {
                        ok = await AddFavoriteAsync(op.GuestId, op.ZoneId, cancellationToken, fromSync: true);
                    }
                    else
                    {
                        ok = await RemoveFavoriteAsync(op.GuestId, op.ZoneId, cancellationToken, fromSync: true);
                    }

                    if (ok)
                    {
                        op.Processed = 1;
                        op.AttemptCount += 1;
                        await _dbService.UpdatePendingOpAsync(op);
                        _logger.LogInformation("[FAV][SYNC] Op Id={Id} ✅ processed", op.Id);
                    }
                    else
                    {
                        op.AttemptCount += 1;
                        op.LastError = "Remote call failed";
                        await _dbService.UpdatePendingOpAsync(op);
                        _logger.LogWarning("[FAV][SYNC] Op Id={Id} ❌ failed, attempt={Attempts}", op.Id, op.AttemptCount);
                    }
                }
                catch (Exception ex)
                {
                    op.AttemptCount += 1;
                    op.LastError = ex.Message;
                    await _dbService.UpdatePendingOpAsync(op);
                    _logger.LogError(ex, "[FAV][SYNC] Op Id={Id} threw exception", op.Id);
                }
            }

            // Pull canonical favorites from server and write to LocalFavorite table
            var guestId = await EnsureGuestIdAsync();
            _logger.LogInformation("[FAV][SYNC] Pulling canonical favorites from server...");
            var favorites = await GetFavoritesAsync(guestId, cancellationToken);
            _logger.LogInformation("[FAV][SYNC] Server returned {Count} favorites", favorites?.Count ?? -1);
            if (favorites != null)
            {
                var localList = favorites.Select(f => new TravelSystem.Shared.Models.LocalFavorite
                {
                    GuestId = f.GuestId,
                    ZoneId = f.ZoneId,
                    CreatedAt = f.CreatedAt,
                    IsDeleted = 0,
                    UpdatedAt = DateTime.UtcNow
                }).ToList();

                await _dbService.ReplaceLocalFavoritesForGuestAsync(guestId, localList);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[API][SYNC] Favorites sync failed");
        }
    }

    private Uri GetBaseUri() => new(ApiConstants.GetBaseApiUrl(), UriKind.Absolute);
    private Uri BuildUri(string relativePath) => new(GetBaseUri(), relativePath);
    private static string TourStopsCacheKey(int tourId, string language) => $"cache_tour_stops_{tourId}_{language}_v4";
    private static string ZoneDetailCacheKey(int zoneId, string language) => $"cache_zone_detail_{zoneId}_{language}_v2";

    private async Task<Uri> BuildToursUriAsync()
    {
        var normalized = await GetCurrentLanguageAsync();
        return BuildUri($"{ApiConstants.ToursEndpoint}?lang={Uri.EscapeDataString(normalized)}");
    }

    private async Task<string> GetCurrentLanguageAsync()
    {
        var storedLanguage = await _dbService.GetSettingAsync("Language", "vi");
        return NormalizeLanguageCode(storedLanguage);
    }

    private static string NormalizeLanguageCode(string? langCode)
    {
        if (string.IsNullOrWhiteSpace(langCode))
        {
            return "vi";
        }

        var normalized = langCode.Trim().ToLowerInvariant();
        var dashIndex = normalized.IndexOf('-');
        if (dashIndex > 0)
        {
            normalized = normalized[..dashIndex];
        }

        return normalized switch
        {
            "en" => "en",
            "ja" => "ja",
            "ko" => "ko",
            _ => "vi"
        };
    }

    private async Task<IReadOnlyList<T>?> LoadCachedListAsync<T>(string key)
    {
        try
        {
            var json = await _dbService.GetSettingAsync(key, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            return JsonSerializer.Deserialize<List<T>>(json, CacheJsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private async Task SaveCachedListAsync<T>(string key, IReadOnlyList<T> items)
    {
        try
        {
            var json = JsonSerializer.Serialize(items, CacheJsonOptions);
            await _dbService.SetSettingAsync(key, json);
        }
        catch
        {
        }
    }

    private async Task<T?> LoadCachedItemAsync<T>(string key)
    {
        try
        {
            var json = await _dbService.GetSettingAsync(key, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
            {
                return default;
            }

            return JsonSerializer.Deserialize<T>(json, CacheJsonOptions);
        }
        catch
        {
            return default;
        }
    }

    private async Task SaveCachedItemAsync<T>(string key, T item)
    {
        try
        {
            var json = JsonSerializer.Serialize(item, CacheJsonOptions);
            await _dbService.SetSettingAsync(key, json);
        }
        catch
        {
        }
    }

    private async Task<IReadOnlyList<TourStopDto>?> FetchTourStopsFromServerAsync(int tourId, string language, CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TourStopsRequestTimeout);

        var stopsUri = BuildUri($"{ApiConstants.ToursEndpoint}/{tourId}/stops?lang={Uri.EscapeDataString(language)}");
        var stops = await _httpClient.GetFromJsonAsync<List<TourStopDto>>(stopsUri, timeoutCts.Token) ?? [];
        foreach (var stop in stops)
        {
            stop.ImageUrl = NormalizeImageUrl(stop.ImageUrl);
        }

        LogTourStopsDiagnostics("SERVER", tourId, language, stops);

        return stops;
    }

    public async Task<IReadOnlyList<TourStopDto>?> GetTourStopsAsync(int tourId, CancellationToken cancellationToken = default)
    {
        var language = await GetCurrentLanguageAsync();
        var cacheKey = TourStopsCacheKey(tourId, language);

        try
        {
            if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
            {
                var stops = await FetchTourStopsFromServerAsync(tourId, language, cancellationToken);
                if (stops is not null)
                {
                    _tourStopsCache[tourId] = stops;
                    await SaveCachedListAsync(cacheKey, stops);
                    _logger.LogInformation("[API] Fetched tour {TourId} stops from server: {StopCount}", tourId, stops.Count);
                    return stops;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[API] Error fetching stops for tour {TourId} from server, falling back to cache", tourId);
        }

        try
        {
            if (_tourStopsCache.TryGetValue(tourId, out var cachedStops) && cachedStops.Count > 0)
            {
                _logger.LogInformation("[API][CACHE] Tour {TourId} stops from memory cache: {StopCount}", tourId, cachedStops.Count);
                LogTourStopsDiagnostics("MEMORY_CACHE", tourId, language, cachedStops);
                return cachedStops;
            }

            var sqliteCachedStops = await LoadCachedListAsync<TourStopDto>(cacheKey);
            if (sqliteCachedStops is { Count: > 0 })
            {
                _tourStopsCache[tourId] = sqliteCachedStops;
                _logger.LogInformation("[API][CACHE] Tour {TourId} stops from SQLite cache: {StopCount}", tourId, sqliteCachedStops.Count);
                LogTourStopsDiagnostics("SQLITE_CACHE", tourId, language, sqliteCachedStops);
                return sqliteCachedStops;
            }

            _logger.LogWarning("[API] No local cached stops found for tour {TourId}", tourId);
            return [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[API] Error while loading stops for tour {TourId} from cache", tourId);
            if (_tourStopsCache.TryGetValue(tourId, out var cachedStops))
            {
                return cachedStops;
            }

            return null;
        }
    }

    public async Task SyncNarrationsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet) return;

            var narrationsUri = BuildUri(ApiConstants.NarrationsEndpoint);
            var narrations = await _httpClient.GetFromJsonAsync<List<Narration>>(narrationsUri, cancellationToken);
            
            if (narrations != null && narrations.Count > 0)
            {
                var localNarrations = narrations.Select(n => new LocalNarration
                {
                    ZoneId = n.ZoneId.ToString(),
                    Language = n.Language,
                    Text = n.Text,
                    FileUrl = n.FileUrl ?? string.Empty, 
                    SyncedAt = DateTime.UtcNow.ToString("O"),
                    UpdatedAt = n.UpdatedAt // Sync thời gian cập nhật từ server
                });

                await _dbService.UpsertNarrationsAsync(localNarrations);
                _logger.LogInformation("[API][SYNC] Synced {Count} narrations", narrations.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[API][SYNC] Narration sync failed");
        }
    }

    public async Task RefreshNarrationAsync(int zoneId, string language, CancellationToken cancellationToken = default)
    {
        try
        {
            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
            {
                return;
            }

            var normalizedLanguage = NormalizeLanguageCode(language);
            var narrationUri = BuildUri($"{ApiConstants.NarrationsEndpoint}?zoneId={zoneId}&language={Uri.EscapeDataString(normalizedLanguage)}");
            var narration = await _httpClient.GetFromJsonAsync<Narration>(narrationUri, cancellationToken);
            if (narration == null)
            {
                return;
            }

            var localNarration = new LocalNarration
            {
                ZoneId = narration.ZoneId.ToString(),
                Language = normalizedLanguage,
                Text = narration.Text,
                FileUrl = narration.FileUrl ?? string.Empty,
                SyncedAt = DateTime.UtcNow.ToString("O"),
                UpdatedAt = narration.UpdatedAt
            };

            await _dbService.UpsertNarrationsAsync(new[] { localNarration });
            await DownloadNarrationAudioAsync(zoneId, normalizedLanguage, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[API][SYNC] Refresh narration failed for ZoneId={ZoneId}, Lang={Language}", zoneId, language);
        }
    }

    private async Task DownloadNarrationAudioAsync(int zoneId, string language, CancellationToken cancellationToken)
    {
        var localNarration = await _dbService.GetNarrationAsync(zoneId, language);
        if (localNarration == null || string.IsNullOrWhiteSpace(localNarration.FileUrl))
        {
            return;
        }

        try
        {
            await _dbService.UpdateDownloadStatusAsync(localNarration.Id, 1);

            var audioDir = Path.Combine(FileSystem.AppDataDirectory, "audio_cache");
            if (!Directory.Exists(audioDir))
            {
                Directory.CreateDirectory(audioDir);
            }

            var fileName = $"{zoneId}_{language}.mp3";
            var localPath = Path.Combine(audioDir, fileName);

            var downloadUrl = localNarration.FileUrl;
            if (!downloadUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                var baseUrl = ApiConstants.GetBaseApiUrl().TrimEnd('/');
                downloadUrl = $"{baseUrl}/{downloadUrl.TrimStart('/')}";
            }

            using var response = await _httpClient.GetAsync(downloadUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                await _dbService.UpdateDownloadStatusAsync(localNarration.Id, 3);
                return;
            }

            await using var fs = new FileStream(localPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await response.Content.CopyToAsync(fs, cancellationToken);
            await _dbService.UpdateDownloadStatusAsync(localNarration.Id, 2, localPath);
        }
        catch
        {
            await _dbService.UpdateDownloadStatusAsync(localNarration.Id, 3);
        }
    }

    public async Task<IReadOnlyList<TourSummaryDto>?> GetToursAsync(CancellationToken cancellationToken = default)

    {
        try
        {
            var toursUri = await BuildToursUriAsync();
            _logger.LogInformation("[API] GetTours network={NetworkAccess}, requesting: {Uri}", Connectivity.Current.NetworkAccess, toursUri);
            using var response = await _httpClient.GetAsync(toursUri, cancellationToken);
            var rawBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[API] Tours request failed: {StatusCode}. Body: {BodySnippet}",
                    (int)response.StatusCode,
                    rawBody.Length > 300 ? rawBody[..300] : rawBody);
                throw new HttpRequestException($"Tours request failed: {(int)response.StatusCode}");
            }

            if (string.IsNullOrWhiteSpace(rawBody) || rawBody.TrimStart().StartsWith("<"))
            {
                _logger.LogWarning("[API] Tours response is not JSON. Body starts with: {BodySnippet}",
                    rawBody.Length > 120 ? rawBody[..120] : rawBody);
                throw new InvalidOperationException("Tours endpoint returned non-JSON content.");
            }

            var tours = JsonSerializer.Deserialize<List<TourSummaryDto>>(rawBody, CacheJsonOptions);
            if (tours is not null)
            {
                foreach (var tour in tours)          
                    tour.ImageUrl = NormalizeImageUrl(tour.ImageUrl);
                await SaveCachedListAsync(ToursCacheKey, tours);
                _toursMemoryCache = tours;
                _logger.LogInformation("[API] Fetched tours from server: {TourCount}", tours.Count);
                return tours;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[API] Error fetching tours from server, falling back to cache");
        }

        try
        {
            if (_toursMemoryCache is { Count: > 0 })
            {
                _logger.LogInformation("[API][CACHE] Tours from memory cache: {TourCount}", _toursMemoryCache.Count);
                return _toursMemoryCache;
            }

            var sqliteCachedTours = await LoadCachedListAsync<TourSummaryDto>(ToursCacheKey);
            if (sqliteCachedTours is { Count: > 0 })
            {
                _toursMemoryCache = sqliteCachedTours;
                _logger.LogInformation("[API][CACHE] Tours from SQLite cache: {TourCount}", sqliteCachedTours.Count);
                return sqliteCachedTours;
            }

            _logger.LogWarning("[API] No local cached tours found");
            return [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[API] Error while loading tours from cache");
            return _toursMemoryCache ?? await LoadCachedListAsync<TourSummaryDto>(ToursCacheKey);
        }
    }

    public async Task SyncCoreDataFromServerIfOnlineAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("[API][SYNC] Startup sync begin");
            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
            {
                _logger.LogInformation("[API][SYNC] Skipped: no internet");
                return;
            }

            var toursUri = await BuildToursUriAsync();
            var tours = await _httpClient.GetFromJsonAsync<List<TourSummaryDto>>(toursUri, cancellationToken) ?? [];
            foreach (var tour in tours)         
                tour.ImageUrl = NormalizeImageUrl(tour.ImageUrl);
            await SaveCachedListAsync(ToursCacheKey, tours);
            _toursMemoryCache = tours;
            _logger.LogInformation("[API][SYNC] Tours synced: {TourCount}", tours.Count);

            foreach (var tour in tours)
            {
                if (cancellationToken.IsCancellationRequested) break;
                var currentLanguage = await GetCurrentLanguageAsync();
                var stops = await FetchTourStopsFromServerAsync(tour.Id, currentLanguage, cancellationToken);
                if (stops is { Count: > 0 })
                {
                    _tourStopsCache[tour.Id] = stops;
                    await SaveCachedListAsync(TourStopsCacheKey(tour.Id, currentLanguage), stops);
                    _logger.LogInformation("[API][SYNC] Tour {TourId} stops synced: {StopCount}", tour.Id, stops.Count);
                }
            }

            // Sync Narrations
            await SyncNarrationsAsync(cancellationToken);

            _logger.LogInformation("[API][SYNC] Startup sync end");

        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[API] Startup sync skipped/failed");
        }
    }

    public async Task<IReadOnlyList<Zone>?> GetZonesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var language = await GetCurrentLanguageAsync();
            var zonesUri = BuildUri($"{ApiConstants.ZonesEndpoint}?lang={Uri.EscapeDataString(language)}");
            Debug.WriteLine($"[API] Loading zones from {zonesUri}");
            var zones = await _httpClient.GetFromJsonAsync<List<Zone>>(zonesUri, cancellationToken) ?? [];
            foreach (var zone in zones)
            {
                zone.ImageUrl = NormalizeImageUrl(zone.ImageUrl);
            }
            _logger.LogInformation("[API] Loaded {ZoneCount} zones", zones.Count);
            return zones;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[API] Error while loading zones");
            return null;
        }
    }

    public async Task<ZoneDetailDto?> GetZoneDetailAsync(int zoneId, CancellationToken cancellationToken = default)
    {
        var language = await GetCurrentLanguageAsync();
        var cacheKey = ZoneDetailCacheKey(zoneId, language);

        try
        {
            if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
            {
                var zoneDetailUri = BuildUri($"{ApiConstants.ZonesEndpoint}/{zoneId}/detail?lang={Uri.EscapeDataString(language)}");
                var detail = await _httpClient.GetFromJsonAsync<ZoneDetailDto>(zoneDetailUri, cancellationToken);
                if (detail != null)
                {
                    detail.ImageUrl = NormalizeImageUrl(detail.ImageUrl);
                    await SaveCachedItemAsync(cacheKey, detail);
                    return detail;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[API] Error loading zone detail {ZoneId} from server, falling back to cache", zoneId);
        }

        try
        {
            var cached = await LoadCachedItemAsync<ZoneDetailDto>(cacheKey);
            if (cached != null)
            {
                cached.ImageUrl = NormalizeImageUrl(cached.ImageUrl);
                return cached;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[API] Error loading zone detail {ZoneId} from cache", zoneId);
        }

        return null;
    }

    private const string GuestIdKey = "current_guest_id";

    public async Task<string> EnsureGuestIdAsync()
    {
        await _guestIdLock.WaitAsync();
        try
        {
            var guestId = Preferences.Default.Get<string>(GuestIdKey, null);
            var isNewGuest = false;
            if (string.IsNullOrEmpty(guestId))
            {
                guestId = Guid.NewGuid().ToString();
                Preferences.Default.Set(GuestIdKey, guestId);
                isNewGuest = true;
            }

            // Keep analytics session identity aligned with guest identity.
            await EnsureAnonymousSessionMatchesGuestAsync(guestId);

            // First-time install should attempt sync immediately (best-effort).
            if (isNewGuest && Guid.TryParse(guestId, out var guestGuid))
            {
                await TrySyncAnonymousInstallAsync(guestGuid);
            }

            return guestId;
        }
        finally
        {
            _guestIdLock.Release();
        }
    }

    public async Task<bool> EnsureAnonymousInstallSyncedAsync(CancellationToken cancellationToken = default)
    {
        var guestId = await EnsureGuestIdAsync();
        if (!Guid.TryParse(guestId, out var guestGuid))
        {
            return false;
        }

        return await TrySyncAnonymousInstallAsync(guestGuid, cancellationToken);
    }

    public async Task<IReadOnlyList<FavoriteDto>?> GetFavoritesAsync(string guestId, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"cache_favorites_{guestId}_v1";
        try
        {
            if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
            {
                var favorites = await _httpClient.GetFromJsonAsync<List<FavoriteDto>>(BuildUri($"api/favorites/{guestId}"), cancellationToken);
                if (favorites is not null)
                {
                    await SaveCachedListAsync(cacheKey, favorites);
                    return favorites;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[API] Error while loading favorites from server, falling back to cache");
        }

        try
        {
            var sqliteCached = await LoadCachedListAsync<FavoriteDto>(cacheKey);
            if (sqliteCached is { Count: > 0 })
            {
                _logger.LogInformation("[API][CACHE] Loaded favorites from cache: {Count}", sqliteCached.Count);
                return sqliteCached;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[API] Error while loading favorites from cache");
        }

        return null;
    }

    public async Task<bool> AddFavoriteAsync(string guestId, int zoneId, CancellationToken cancellationToken = default, bool fromSync = false)
    {
        _logger.LogDebug("[FAV] AddFavorite START — guestId={GuestId}, zoneId={ZoneId}", guestId, zoneId);
        await _dbService.InitializeAsync();
        await _dbService.InsertOrUpdateLocalFavoriteAsync(new LocalFavorite
        {
            GuestId = guestId,
            ZoneId = zoneId,
            CreatedAt = DateTime.UtcNow,
            IsDeleted = 0,
            UpdatedAt = DateTime.UtcNow
        });
        _logger.LogDebug("[FAV] LocalFavorite upserted — zoneId={ZoneId}", zoneId);
        if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
        {
            if (!fromSync)
            {
                await _dbService.InsertPendingOpAsync(new PendingFavoriteOp
                {
                    GuestId = guestId,
                    ZoneId = zoneId,
                    Operation = FavoriteOperation.Add,
                    CreatedAt = DateTime.UtcNow
                });
            }
            _logger.LogInformation("[FAV] OFFLINE — PendingOp(Add) queued, zoneId={ZoneId}", zoneId); ;
            return true; // trả true vì đã lưu local
        }

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                BuildUri(ApiConstants.FavoritesEndpoint),
                new { GuestId = guestId, ZoneId = zoneId },
                cancellationToken);
            _logger.LogInformation("[FAV] POST {Endpoint} → {StatusCode}",
            ApiConstants.FavoritesEndpoint, (int)response.StatusCode);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return true;
            if (!response.IsSuccessStatusCode && !fromSync)
            {
                // Server thất bại → queue lại
                await _dbService.InsertPendingOpAsync(new PendingFavoriteOp
                {
                    GuestId = guestId,
                    ZoneId = zoneId,
                    Operation = FavoriteOperation.Add,
                    CreatedAt = DateTime.UtcNow
                });
                _logger.LogWarning("[FAV] Server failed ({StatusCode}) — PendingOp(Add) queued", (int)response.StatusCode);
            }

            return response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.NotFound; ;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[FAV] Exception during POST — PendingOp(Add) queued, zoneId={ZoneId}", zoneId);
            if (!fromSync)
            {
                await _dbService.InsertPendingOpAsync(new PendingFavoriteOp
                {
                    GuestId = guestId,
                    ZoneId = zoneId,
                    Operation = FavoriteOperation.Add,
                    CreatedAt = DateTime.UtcNow
                });
            }
            return true; // local đã lưu rồi
        }
    }

    public async Task<bool> RemoveFavoriteAsync(string guestId, int zoneId, CancellationToken cancellationToken = default, bool fromSync = false)
    {
        _logger.LogDebug("[FAV] RemoveFavorite START — guestId={GuestId}, zoneId={ZoneId}", guestId, zoneId);
        // Optimistic local delete
        await _dbService.InitializeAsync();
        await _dbService.MarkLocalFavoriteDeletedAsync(guestId, zoneId);
        _logger.LogDebug("[FAV] LocalFavorite soft-deleted — zoneId={ZoneId}", zoneId);

        if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
        {
            if(!fromSync)
            {
                await _dbService.InsertPendingOpAsync(new PendingFavoriteOp
                {
                    GuestId = guestId,
                    ZoneId = zoneId,
                    Operation = FavoriteOperation.Remove,
                    CreatedAt = DateTime.UtcNow
                });
            }    
            _logger.LogInformation("[FAV] OFFLINE — PendingOp(Remove) queued, zoneId={ZoneId}", zoneId);
            return true;
        }

        try
        {
            var response = await _httpClient.DeleteAsync(
                BuildUri($"{ApiConstants.FavoritesEndpoint}/{guestId}/{zoneId}"),
                cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return true;

            if (!response.IsSuccessStatusCode && !fromSync)
            {
                await _dbService.InsertPendingOpAsync(new PendingFavoriteOp
                {
                    GuestId = guestId,
                    ZoneId = zoneId,
                    Operation = FavoriteOperation.Remove,
                    CreatedAt = DateTime.UtcNow
                });
                _logger.LogWarning("[FAV] Server failed ({StatusCode}) — PendingOp(Remove) queued", (int)response.StatusCode);
            }
            

            return response.IsSuccessStatusCode
                || response.StatusCode == System.Net.HttpStatusCode.NotFound; ;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[FAV] Exception during DELETE — PendingOp(Remove) queued, zoneId={ZoneId}", zoneId);
            if (!fromSync)
            {
                await _dbService.InsertPendingOpAsync(new PendingFavoriteOp
                {
                    GuestId = guestId,
                    ZoneId = zoneId,
                    Operation = FavoriteOperation.Remove,
                    CreatedAt = DateTime.UtcNow
                });
            }
            return true;
        }
    }
    public async Task<bool> RegisterAnonymousInstallAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = new AnonymousInstallRequest
            {
                SessionId = sessionId,
                CreatedAt = DateTime.UtcNow
            };

            var response = await _httpClient.PostAsJsonAsync(BuildUri($"{ApiConstants.AnalyticsEndpoint}/install"), payload, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[API] Failed to register anonymous app install");
            return false;
        }
    }

    public async Task<bool> TrackEnterZoneAsync(int zoneId, double latitude, double longitude, CancellationToken cancellationToken = default)
    {
        return await TrackAnalyticsEventAsync("EnterZone", zoneId, latitude, longitude, 0, cancellationToken);
    }

    public async Task<bool> TrackPlayNarrationAsync(int zoneId, int dwellTimeSeconds, CancellationToken cancellationToken = default)
    {
        return await TrackPlayNarrationAsync(zoneId, dwellTimeSeconds, null, cancellationToken);
    }

    public async Task<bool> TrackPlayNarrationAsync(int zoneId, int dwellTimeSeconds, string? language, CancellationToken cancellationToken = default)
    {
        var lang = (language ?? string.Empty).Trim().ToLowerInvariant();
        var actionType = string.IsNullOrWhiteSpace(lang) ? "PlayNarration" : $"PlayNarration|{lang}";
        return await TrackAnalyticsEventAsync(actionType, zoneId, 0, 0, Math.Max(0, dwellTimeSeconds), cancellationToken);
    }

    public async Task<bool> TrackLocationPingAsync(double latitude, double longitude, int? zoneId = null, CancellationToken cancellationToken = default)
    {
        return await TrackAnalyticsEventAsync("LocationPing", zoneId, latitude, longitude, 0, cancellationToken);
    }

    private async Task<Guid> EnsureAnalyticsSessionIdAsync()
    {
        await _dbService.InitializeAsync();

        var sessionIdValue = await _dbService.GetSettingAsync("AnonymousSessionId", string.Empty);
        if (Guid.TryParse(sessionIdValue, out var sessionId))
        {
            return sessionId;
        }

        // Always prefer using GuestId so AppInstall + analytics events share one identity.
        var guestId = await EnsureGuestIdAsync();
        var hasGuestGuid = Guid.TryParse(guestId, out var guestGuid);

        await _analyticsSessionLock.WaitAsync();
        try
        {
            // Re-check after entering lock to prevent first-run race creating multiple IDs.
            sessionIdValue = await _dbService.GetSettingAsync("AnonymousSessionId", string.Empty);
            if (Guid.TryParse(sessionIdValue, out sessionId))
            {
                return sessionId;
            }

            if (hasGuestGuid)
            {
                await _dbService.SetSettingAsync("AnonymousSessionId", guestGuid.ToString());
                await _dbService.SetSettingAsync("AnonymousInstallSynced", "0");
                return guestGuid;
            }

            sessionId = Guid.NewGuid();
            await _dbService.SetSettingAsync("AnonymousSessionId", sessionId.ToString());
            await _dbService.SetSettingAsync("AnonymousInstallSynced", "0");
            return sessionId;
        }
        finally
        {
            _analyticsSessionLock.Release();
        }
    }

    private async Task EnsureAnonymousSessionMatchesGuestAsync(string guestId)
    {
        if (!Guid.TryParse(guestId, out var guestGuid))
        {
            return;
        }

        await _dbService.InitializeAsync();
        await _analyticsSessionLock.WaitAsync();
        try
        {
            var sessionIdValue = await _dbService.GetSettingAsync("AnonymousSessionId", string.Empty);
            if (!Guid.TryParse(sessionIdValue, out var existing) || existing != guestGuid)
            {
                await _dbService.SetSettingAsync("AnonymousSessionId", guestGuid.ToString());
                await _dbService.SetSettingAsync("AnonymousInstallSynced", "0");
            }
        }
        finally
        {
            _analyticsSessionLock.Release();
        }
    }

    private async Task<bool> TrySyncAnonymousInstallAsync(Guid guestGuid, CancellationToken cancellationToken = default)
    {
        await _dbService.InitializeAsync();

        await _installSyncLock.WaitAsync(cancellationToken);
        try
        {
            var installSynced = await _dbService.GetSettingAsync("AnonymousInstallSynced", "0");
            if (installSynced == "1")
            {
                return true;
            }

            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
            {
                return false;
            }

            var synced = await RegisterAnonymousInstallAsync(guestGuid, cancellationToken);
            if (synced)
            {
                await _dbService.SetSettingAsync("AnonymousInstallSynced", "1");
            }

            return synced;
        }
        finally
        {
            _installSyncLock.Release();
        }
    }

    public async Task SyncAnalyticsIfOnlineAsync(CancellationToken cancellationToken = default)
    {
        if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
        {
            _logger.LogInformation("[API][ANALYTICS_SYNC] Skipped: offline");
            return;
        }

        await _dbService.InitializeAsync();
        await _analyticsSyncLock.WaitAsync(cancellationToken);
        try
        {
            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
            {
                return;
            }

            var pending = await _dbService.GetUnsyncedAnalyticsAsync();
            if (pending.Count == 0)
            {
                return;
            }

            _logger.LogInformation("[API][ANALYTICS_SYNC] Pending events: {Count}", pending.Count);

            foreach (var localEvent in pending)
            {
                if (cancellationToken.IsCancellationRequested
                    || Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
                {
                    break;
                }

                if (!TryBuildAnalyticsPayload(localEvent, out var payload))
                {
                    // Invalid local data should not block later events forever.
                    await _dbService.MarkLocalAnalyticsSyncedAsync(localEvent.Id);
                    continue;
                }

                try
                {
                    var response = await _httpClient.PostAsJsonAsync(
                        BuildUri($"{ApiConstants.AnalyticsEndpoint}/event"),
                        payload,
                        cancellationToken);

                    if (response.IsSuccessStatusCode)
                    {
                        await _dbService.MarkLocalAnalyticsSyncedAsync(localEvent.Id);
                        continue;
                    }

                    var statusCode = (int)response.StatusCode;
                    if (statusCode >= 400 && statusCode < 500 && statusCode != 408 && statusCode != 429)
                    {
                        // Permanent client-side failure: mark as synced to avoid endless retry loop.
                        await _dbService.MarkLocalAnalyticsSyncedAsync(localEvent.Id);
                        _logger.LogWarning("[API][ANALYTICS_SYNC] Dropped invalid event Id={Id} Status={StatusCode}", localEvent.Id, statusCode);
                        continue;
                    }

                    _logger.LogWarning("[API][ANALYTICS_SYNC] Stop sync on transient failure Status={StatusCode}", statusCode);
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[API][ANALYTICS_SYNC] Stop sync on exception for event Id={Id}", localEvent.Id);
                    break;
                }
            }
        }
        finally
        {
            _analyticsSyncLock.Release();
        }
    }

    public async Task<bool> TrackAnalyticsEventAsync(
        string actionType,
        int? zoneId,
        double latitude,
        double longitude,
        int dwellTimeSeconds = 0,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!IsAllowedAnalyticsAction(actionType))
            {
                return false;
            }

            var sessionId = await EnsureAnalyticsSessionIdAsync();
            var nowUtc = DateTime.UtcNow;
            var localEvent = new LocalAnalytics
            {
                SessionId = sessionId.ToString(),
                ZoneId = zoneId.HasValue ? zoneId.Value.ToString() : string.Empty,
                Latitude = latitude,
                Longitude = longitude,
                ActionType = actionType,
                DwellTimeSeconds = Math.Max(0, dwellTimeSeconds),
                Timestamp = nowUtc.ToString("o"),
                IsSynced = 0
            };

            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
            {
                await _dbService.InsertLocalAnalyticsAsync(localEvent);
                return true;
            }

            var payload = new AnalyticsEventRequest
            {
                SessionId = sessionId,
                ZoneId = zoneId,
                Latitude = latitude,
                Longitude = longitude,
                ActionType = actionType,
                DwellTimeSeconds = Math.Max(0, dwellTimeSeconds),
                CreatedAt = nowUtc
            };

            var response = await _httpClient.PostAsJsonAsync(
                BuildUri($"{ApiConstants.AnalyticsEndpoint}/event"),
                payload,
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            var statusCode = (int)response.StatusCode;
            if (statusCode >= 400 && statusCode < 500 && statusCode != 408 && statusCode != 429)
            {
                // Validation/client errors should surface instead of being retried forever.
                return false;
            }

            await _dbService.InsertLocalAnalyticsAsync(localEvent);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[API] Failed to track analytics event {ActionType}", actionType);
            try
            {
                var fallbackSessionId = await EnsureAnalyticsSessionIdAsync();
                await _dbService.InsertLocalAnalyticsAsync(new LocalAnalytics
                {
                    SessionId = fallbackSessionId.ToString(),
                    ZoneId = zoneId.HasValue ? zoneId.Value.ToString() : string.Empty,
                    Latitude = latitude,
                    Longitude = longitude,
                    ActionType = actionType,
                    DwellTimeSeconds = Math.Max(0, dwellTimeSeconds),
                    Timestamp = DateTime.UtcNow.ToString("o"),
                    IsSynced = 0
                });
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    private static bool TryBuildAnalyticsPayload(LocalAnalytics localEvent, out AnalyticsEventRequest payload)
    {
        payload = new AnalyticsEventRequest();

        if (!Guid.TryParse(localEvent.SessionId, out var sessionId))
        {
            return false;
        }

        if (!IsAllowedAnalyticsAction(localEvent.ActionType))
        {
            return false;
        }

        int? zoneId = null;
        if (!string.IsNullOrWhiteSpace(localEvent.ZoneId))
        {
            if (!int.TryParse(localEvent.ZoneId, out var parsedZoneId) || parsedZoneId <= 0)
            {
                return false;
            }
            zoneId = parsedZoneId;
        }

        if (!DateTime.TryParse(localEvent.Timestamp, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var createdAt))
        {
            createdAt = DateTime.UtcNow;
        }

        payload = new AnalyticsEventRequest
        {
            SessionId = sessionId,
            ZoneId = zoneId,
            Latitude = localEvent.Latitude,
            Longitude = localEvent.Longitude,
            ActionType = localEvent.ActionType,
            DwellTimeSeconds = Math.Max(0, localEvent.DwellTimeSeconds),
            CreatedAt = createdAt
        };

        return true;
    }


    private string NormalizeImageUrl(string? imageUrl)
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
                var baseUri = GetBaseUri();
                if (baseUri is null)
                {
                    return string.Empty;
                }

                var filePath = absoluteUri.LocalPath.Replace('\\', '/').TrimStart('/');
                return new Uri(baseUri, filePath).ToString();
            }

            if (absoluteUri.Scheme != Uri.UriSchemeHttp && absoluteUri.Scheme != Uri.UriSchemeHttps)
            {
                return string.Empty;
            }

            if ((absoluteUri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
                || absoluteUri.Host.Equals("127.0.0.1"))
                )
            {
                var baseUri = GetBaseUri();
                return new UriBuilder(absoluteUri)
                {
                    Scheme = baseUri.Scheme,
                    Host = baseUri.Host,
                    Port = baseUri.Port
                }.Uri.ToString();
            }

            return absoluteUri.ToString();
        }

        var relativePath = imageUrl.TrimStart('~', '/');
        return new Uri(GetBaseUri(), relativePath).ToString();
    }

    private void LogTourStopsDiagnostics(string source, int tourId, string language, IReadOnlyList<TourStopDto> stops)
    {
        var preview = string.Join(" | ", stops
            .OrderBy(s => s.OrderIndex)
            .Take(6)
            .Select(s =>
            {
                var desc = string.IsNullOrWhiteSpace(s.Description)
                    ? "(empty)"
                    : (s.Description!.Length > 45 ? s.Description[..45] + "..." : s.Description);
                return $"ZoneId={s.ZoneId},Idx={s.OrderIndex},Name='{s.Name}',Desc='{desc}'";
            }));

        _logger.LogInformation("[API][STOPS_DIAG] Source={Source} TourId={TourId} Lang={Lang} Count={Count} Preview={Preview}",
            source,
            tourId,
            language,
            stops.Count,
            preview);
    }

    private sealed class ZoneSummaryDto
    {
        public int ZoneType { get; set; }
        public int ActiveTime { get; set; }
    }

    private sealed class AnonymousInstallRequest
    {
        public Guid SessionId { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    private sealed class AnalyticsEventRequest
    {
        public Guid SessionId { get; set; }
        public int? ZoneId { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string ActionType { get; set; } = string.Empty;
        public int DwellTimeSeconds { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    private static bool IsAllowedAnalyticsAction(string actionType)
    {
        if (AllowedAnalyticsActions.Contains(actionType))
        {
            return true;
        }

        return actionType.StartsWith("PlayNarration|", StringComparison.OrdinalIgnoreCase);
    }
}

public class FavoriteDto
{
    public int Id { get; set; }
    public string GuestId { get; set; }
    public int ZoneId { get; set; }
    public DateTime CreatedAt { get; set; }
    public FavoriteZoneDto Zone { get; set; }
}

public class FavoriteZoneDto
{
    public string Name { get; set; }
    public string Description { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int? ShopId { get; set; }
}

public class TourSummaryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public int Duration { get; set; }
    public int StopsCount { get; set; }
}

public class TourStopDto
{
    public int ZoneId { get; set; }
    public int OrderIndex { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int Radius { get; set; }
    public bool IsMain { get; set; }
    public string? Address { get; set; }
    public string? Hours { get; set; }
}

public class ZoneDetailDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int Radius { get; set; }
    public string? Address { get; set; }
    public string? Hours { get; set; }
}

