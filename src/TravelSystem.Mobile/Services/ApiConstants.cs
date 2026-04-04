namespace TravelSystem.Mobile.Services;

public static class ApiConstants
{
#if DEBUG
    // Debug can target the deployed web API directly.
    private const string DebugTunnelBaseApiUrl = "https://nonstereotyped-biometrical-amir.ngrok-free.dev/";
#else
    private const string ProductionBaseApiUrl = "https://nonstereotyped-biometrical-amir.ngrok-free.dev/";
#endif

    public static string GetBaseApiUrl()
    {
#if DEBUG
        return DebugTunnelBaseApiUrl;
#else
        return ProductionBaseApiUrl;
#endif
    }

    public const string RoutesEndpoint     = "api/routes";
    public const string ZonesEndpoint      = "api/zones";
    public const string ToursEndpoint      = "api/tours";
    public const string NarrationsEndpoint = "api/narrations";
    public const string AnalyticsEndpoint  = "api/analytics";
    public const string FavoritesEndpoint = "api/favorites";
}
