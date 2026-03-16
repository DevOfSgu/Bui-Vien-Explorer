using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Net.Http.Json;
using TravelSystem.Shared.Models;

namespace TravelSystem.Mobile.Services;

public class ApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ApiService> _logger;

    public ApiService(ILogger<ApiService> logger)
    {
        _logger = logger;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(ApiConstants.BaseApiUrl),
            Timeout = TimeSpan.FromSeconds(8)
        };
    }

    public async Task<IReadOnlyList<Zone>?> GetZonesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            Debug.WriteLine($"[API] Loading zones from {_httpClient.BaseAddress}{ApiConstants.ZonesEndpoint}");
            var zones = await _httpClient.GetFromJsonAsync<List<Zone>>(ApiConstants.ZonesEndpoint, cancellationToken) ?? [];
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
            var favorites = await _httpClient.GetFromJsonAsync<List<FavoriteDto>>($"api/favorites/{guestId}", cancellationToken);
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
            var response = await _httpClient.PostAsJsonAsync("api/favorites", new { GuestId = guestId, ZoneId = zoneId }, cancellationToken);
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
            var response = await _httpClient.DeleteAsync($"api/favorites/{guestId}/{zoneId}", cancellationToken);
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

            var response = await _httpClient.PostAsJsonAsync($"{ApiConstants.AnalyticsEndpoint}/install", payload, cancellationToken);
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
                if (_httpClient.BaseAddress is null)
                {
                    return string.Empty;
                }

                var filePath = absoluteUri.LocalPath.Replace('\\', '/').TrimStart('/');
                return new Uri(_httpClient.BaseAddress, filePath).ToString();
            }

            if (absoluteUri.Scheme != Uri.UriSchemeHttp && absoluteUri.Scheme != Uri.UriSchemeHttps)
            {
                return string.Empty;
            }

            if ((absoluteUri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
                || absoluteUri.Host.Equals("127.0.0.1"))
                && _httpClient.BaseAddress is not null)
            {
                var baseUri = _httpClient.BaseAddress;
                return new UriBuilder(absoluteUri)
                {
                    Scheme = baseUri.Scheme,
                    Host = baseUri.Host,
                    Port = baseUri.Port
                }.Uri.ToString();
            }

            return absoluteUri.ToString();
        }

        if (_httpClient.BaseAddress is null)
        {
            return imageUrl;
        }

        var relativePath = imageUrl.TrimStart('~', '/');
        return new Uri(_httpClient.BaseAddress!, relativePath).ToString();
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

