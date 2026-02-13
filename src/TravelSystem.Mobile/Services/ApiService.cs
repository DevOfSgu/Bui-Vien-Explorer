namespace TravelSystem.Mobile.Services;

public class ApiService
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl = "http://10.0.2.2:5281"; // Android Emulator → Host PC

    public ApiService()
    {
        _httpClient = new HttpClient();
    }

    public async Task<string> TestConnectionAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/routes");
            response.EnsureSuccessStatusCode();
            var data = await response.Content.ReadAsStringAsync();
            return data;
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }
}