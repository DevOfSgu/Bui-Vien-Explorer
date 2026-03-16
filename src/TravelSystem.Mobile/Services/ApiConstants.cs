namespace TravelSystem.Mobile.Services;

public static class ApiConstants
{
#if DEBUG
    // Android Emulator: 10.0.2.2 trỏ về localhost của máy host
    public const string BaseApiUrl = "http://10.0.2.2:5281";
#else
    // Production: thay bằng URL thật khi deploy
    public const string BaseApiUrl = "https://YOUR_NGROK_URL_HERE.ngrok-free.app/";
#endif

    public const string RoutesEndpoint     = "api/routes";
    public const string ZonesEndpoint      = "api/zones";
    public const string NarrationsEndpoint = "api/narrations";
    public const string AnalyticsEndpoint  = "api/analytics";
}
