namespace TravelSystem.Mobile.Services;

public static class ApiConstants
{
#if DEBUG
    private const string DebugAndroidEmulatorBaseApiUrl = "http://10.0.2.2:5281/";
    private const string DebugAndroidDeviceBaseApiUrl = "http://127.0.0.1:5281/";
    private const string DebugDesktopBaseApiUrl = "http://localhost:5281/";
#else
    // Production: thay bằng URL thật khi deploy
    private const string ProductionBaseApiUrl = "https://YOUR_NGROK_URL_HERE.ngrok-free.app/";
#endif

    public static string GetBaseApiUrl()
    {
#if DEBUG
#if ANDROID
        return DeviceInfo.DeviceType == DeviceType.Virtual
            ? DebugAndroidEmulatorBaseApiUrl
            : DebugAndroidDeviceBaseApiUrl;
#else
        return DebugDesktopBaseApiUrl;
#endif
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
