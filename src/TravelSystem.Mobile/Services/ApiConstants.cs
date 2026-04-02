namespace TravelSystem.Mobile.Services;

public static class ApiConstants
{
#if DEBUG
    // For debug testing on real devices/emulators via ngrok tunnel.
    // Update this URL whenever ngrok forwarding domain changes.
    private const string DebugTunnelBaseApiUrl = "https://nonstereotyped-biometrical-amir.ngrok-free.dev/";
#else
    // Production: replace with your deployed API domain.
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
