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
}
