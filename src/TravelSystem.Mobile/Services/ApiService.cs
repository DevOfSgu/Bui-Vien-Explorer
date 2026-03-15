using Microsoft.Extensions.Logging;
using System.Diagnostics;

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

    public async Task<string> TestConnectionAsync()
    {
        var endpoint = ApiConstants.RoutesEndpoint;

        try
        {
            Debug.WriteLine($"[API] Sending GET request to {_httpClient.BaseAddress}{endpoint}");
            _logger.LogInformation("[API] Sending GET request to {BaseAddress}{Endpoint}", _httpClient.BaseAddress, endpoint);

            var response = await _httpClient.GetAsync(endpoint);

            Debug.WriteLine($"[API] Received status {(int)response.StatusCode} from {endpoint}");
            _logger.LogInformation("[API] Received response {StatusCode} from {Endpoint}", (int)response.StatusCode, endpoint);

            response.EnsureSuccessStatusCode();

            var data = await response.Content.ReadAsStringAsync();

            var preview = data.Length > 500 ? data[..500] + "..." : data;
            Debug.WriteLine($"[API] Response payload preview: {preview}");
            _logger.LogDebug("[API] Response payload from {Endpoint}: {Payload}", endpoint, preview);

            return data;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[API] Error while calling {_httpClient.BaseAddress}{endpoint}: {ex}");
            _logger.LogError(ex, "[API] Error while calling {BaseAddress}{Endpoint}", _httpClient.BaseAddress, endpoint);
            return $"Error: {ex.Message}";
        }
    }
}