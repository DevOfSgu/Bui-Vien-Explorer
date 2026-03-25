using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Linq;
using TravelSystem.Shared.Models;

namespace TravelSystem.Mobile.Services;

public class ApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ApiService> _logger;
    private readonly DatabaseService _dbService;
    private readonly ConcurrentDictionary<int, IReadOnlyList<TourStopDto>> _tourStopsCache = new();
    private IReadOnlyList<TourSummaryDto>? _toursMemoryCache;
    private static readonly TimeSpan TourStopsRequestTimeout = TimeSpan.FromSeconds(3);
    private static readonly JsonSerializerOptions CacheJsonOptions = new() { PropertyNameCaseInsensitive = true };
    private const string ToursCacheKey = "cache_tours_v1";

    public ApiService(ILogger<ApiService> logger, DatabaseService dbService)
    {
        _logger = logger;
        _dbService = dbService;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30) // Increased for initial sync
        };
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
    private static string TourStopsCacheKey(int tourId) => $"cache_tour_stops_{tourId}_v1";

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

    private async Task<IReadOnlyList<TourStopDto>?> FetchTourStopsFromServerAsync(int tourId, CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TourStopsRequestTimeout);

        var stopsUri = BuildUri($"{ApiConstants.ToursEndpoint}/{tourId}/stops");
        var stops = await _httpClient.GetFromJsonAsync<List<TourStopDto>>(stopsUri, timeoutCts.Token) ?? [];
        foreach (var stop in stops)
        {
            stop.ImageUrl = NormalizeImageUrl(stop.ImageUrl);
        }

        return stops;
    }

    public async Task<IReadOnlyList<TourStopDto>?> GetTourStopsAsync(int tourId, CancellationToken cancellationToken = default)
    {
        try
        {
            if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
            {
                var stops = await FetchTourStopsFromServerAsync(tourId, cancellationToken);
                if (stops is not null)
                {
                    _tourStopsCache[tourId] = stops;
                    await SaveCachedListAsync(TourStopsCacheKey(tourId), stops);
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
                return cachedStops;
            }

            var sqliteCachedStops = await LoadCachedListAsync<TourStopDto>(TourStopsCacheKey(tourId));
            if (sqliteCachedStops is { Count: > 0 })
            {
                _tourStopsCache[tourId] = sqliteCachedStops;
                _logger.LogInformation("[API][CACHE] Tour {TourId} stops from SQLite cache: {StopCount}", tourId, sqliteCachedStops.Count);
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
                    FileUrl = n.FileUrl ?? string.Empty, // Quan trọng: Sync URL từ server
                    Version = string.IsNullOrEmpty(n.FileUrl) ? 1 : 2, // Nếu có file thì coi như version mới
                    SyncedAt = DateTime.UtcNow.ToString("O")
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

    public async Task<IReadOnlyList<TourSummaryDto>?> GetToursAsync(CancellationToken cancellationToken = default)

    {
        try
        {
            if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
            {

                var toursUri = BuildUri(ApiConstants.ToursEndpoint);
                _logger.LogInformation("[API] Requesting: {Uri}", toursUri);
                var tours = await _httpClient.GetFromJsonAsync<List<TourSummaryDto>>(toursUri, cancellationToken);
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

            var toursUri = BuildUri(ApiConstants.ToursEndpoint);
            var tours = await _httpClient.GetFromJsonAsync<List<TourSummaryDto>>(toursUri, cancellationToken) ?? [];
            foreach (var tour in tours)         
                tour.ImageUrl = NormalizeImageUrl(tour.ImageUrl);
            await SaveCachedListAsync(ToursCacheKey, tours);
            _toursMemoryCache = tours;
            _logger.LogInformation("[API][SYNC] Tours synced: {TourCount}", tours.Count);

            foreach (var tour in tours)
            {
                if (cancellationToken.IsCancellationRequested) break;
                var stops = await FetchTourStopsFromServerAsync(tour.Id, cancellationToken);
                if (stops is { Count: > 0 })
                {
                    _tourStopsCache[tour.Id] = stops;
                    await SaveCachedListAsync(TourStopsCacheKey(tour.Id), stops);
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
            var zonesUri = BuildUri(ApiConstants.ZonesEndpoint);
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

    private const string GuestIdKey = "current_guest_id";

    public async Task<string> EnsureGuestIdAsync()
    {
        var guestId = Preferences.Default.Get<string>(GuestIdKey, null);
        if (string.IsNullOrEmpty(guestId))
        {
            guestId = Guid.NewGuid().ToString();
            Preferences.Default.Set(GuestIdKey, guestId);
            
            // Register install if needed
            await RegisterAnonymousInstallAsync(Guid.Parse(guestId));
        }
        return guestId;
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
}

