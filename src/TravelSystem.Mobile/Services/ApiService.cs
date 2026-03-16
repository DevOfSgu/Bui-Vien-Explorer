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

<<<<<<< HEAD
    public async Task<IReadOnlyList<Zone>?> GetZonesAsync(CancellationToken cancellationToken = default)
=======
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

    public async Task<RouteSyncData?> GetRouteSyncDataAsync(string? lastSyncedAt, CancellationToken cancellationToken = default)
    {
        var endpoint = string.IsNullOrWhiteSpace(lastSyncedAt)
            ? $"{ApiConstants.RoutesEndpoint}/sync"
            : $"{ApiConstants.RoutesEndpoint}/sync?lastSyncedAt={Uri.EscapeDataString(lastSyncedAt)}";

        var response = await _httpClient.GetFromJsonAsync<RouteSyncResponse>(endpoint, cancellationToken);
        if (response is null || !response.HasUpdates || response.Data is null)
        {
            return null;
        }

        var normalizedRoutes = response.Data.Routes
            .Select(r =>
            {
                r.ImageUrl = NormalizeImageUrl(r.ImageUrl);
                return r;
            })
            .ToList();

        return new RouteSyncData(
            response.Timestamp,
            normalizedRoutes,
            response.Data.Zones,
            response.Data.Narrations);
    }

    public async Task<IReadOnlyList<RouteSummary>> GetRouteSummariesAsync()
>>>>>>> ad4c67a23af7236728f0c13bdf9c7f329fdd0da9
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
            throw;
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
