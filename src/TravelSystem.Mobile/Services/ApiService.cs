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
            BaseAddress = new Uri(ApiConstants.BaseApiUrl)
        };
    }

    public async Task<IReadOnlyList<RouteSummary>> GetRouteSummariesAsync()
    {
        try
        {
            Debug.WriteLine($"[API] Loading routes from {_httpClient.BaseAddress}{ApiConstants.RoutesEndpoint}");
            var routes = await _httpClient.GetFromJsonAsync<List<Routes>>(ApiConstants.RoutesEndpoint) ?? [];

            var results = new List<RouteSummary>();
            foreach (var route in routes)
            {
                var zonesEndpoint = $"{ApiConstants.ZonesEndpoint}?routeId={route.Id}";
                var zones = await _httpClient.GetFromJsonAsync<List<ZoneSummaryDto>>(zonesEndpoint) ?? [];

                var stopCount = zones.Count;
                var minutes = zones.Sum(z => z.ActiveTime > 0 ? z.ActiveTime : 0);
                if (minutes <= 0)
                {
                    minutes = stopCount * 8;
                }

                var zoneType = zones.FirstOrDefault()?.ZoneType ?? 0;

                results.Add(new RouteSummary(
                    route.Id,
                    route.Name,
                    route.Description,
                    stopCount,
                    minutes,
                    zoneType));
            }

            _logger.LogInformation("[API] Loaded {RouteCount} routes for home screen", results.Count);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[API] Error while loading route summaries");
            throw;
        }
    }

    private sealed class ZoneSummaryDto
    {
        public int ZoneType { get; set; }
        public int ActiveTime { get; set; }
    }
    }

public sealed record RouteSummary(
    int RouteId,
    string Name,
    string Description,
    int StopCount,
    int DurationMinutes,
    int ZoneType);
