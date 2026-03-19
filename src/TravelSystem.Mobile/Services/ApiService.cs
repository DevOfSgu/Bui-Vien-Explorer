using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Collections.Concurrent;
using System.Text.Json;
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
            Timeout = TimeSpan.FromSeconds(8)
        };
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

    public async Task<IReadOnlyList<TourSummaryDto>?> GetToursAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
            {
                var toursUri = BuildUri(ApiConstants.ToursEndpoint);
                var tours = await _httpClient.GetFromJsonAsync<List<TourSummaryDto>>(toursUri, cancellationToken);
                if (tours is not null)
                {
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
        try
        {
            var favorites = await _httpClient.GetFromJsonAsync<List<FavoriteDto>>(BuildUri($"api/favorites/{guestId}"), cancellationToken);
            return favorites;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[API] Error while loading favorites");
            return null;
        }
    }

    public async Task<bool> AddFavoriteAsync(string guestId, int zoneId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(BuildUri("api/favorites"), new { GuestId = guestId, ZoneId = zoneId }, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[API] Error while adding favorite");
            return false;
        }
    }

    public async Task<bool> RemoveFavoriteAsync(string guestId, int zoneId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.DeleteAsync(BuildUri($"api/favorites/{guestId}/{zoneId}"), cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[API] Error while removing favorite");
            return false;
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
}

