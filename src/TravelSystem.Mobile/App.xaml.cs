using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using TravelSystem.Mobile.Services;
using TravelSystem.Mobile.ViewModels;

namespace TravelSystem.Mobile;

public partial class App : Application
{
    private readonly DatabaseService _dbService;
    private readonly ApiService _apiService;
    private readonly TourDetailViewModel _tourDetailViewModel;

    public App(DatabaseService dbService, ApiService apiService, TourDetailViewModel tourDetailViewModel)
    {
        InitializeComponent();
        _dbService = dbService;
        _apiService = apiService;
        _tourDetailViewModel = tourDetailViewModel;
        Debug.WriteLine("✅ App initialized successfully");
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new AppShell());
    }

    protected override void OnStart()
    {
        base.OnStart();
        _ = InitializeAppAsync();
        Connectivity.Current.ConnectivityChanged += async (s, e) =>
        {
            if (e.NetworkAccess == NetworkAccess.Internet)
                await _apiService.SyncFavoritesIfOnlineAsync();
        };
    }

    private async Task InitializeAppAsync()
    {
        try
        {
            await _dbService.InitializeAsync();

            var sessionIdValue = await _dbService.GetSettingAsync("AnonymousSessionId", string.Empty);
            if (!Guid.TryParse(sessionIdValue, out var sessionId))
            {
                sessionId = Guid.NewGuid();
                await _dbService.SetSettingAsync("AnonymousSessionId", sessionId.ToString());
                await _dbService.SetSettingAsync("AnonymousInstallSynced", "0");
            }

            var installSynced = await _dbService.GetSettingAsync("AnonymousInstallSynced", "0");
            if (installSynced != "1")
            {
                var synced = await _apiService.RegisterAnonymousInstallAsync(sessionId);
                if (synced)
                {
                    await _dbService.SetSettingAsync("AnonymousInstallSynced", "1");
                }
            }

            // Pre-warm GPS permission sớm — để TourDetailPage không xin quyền lần đầu khi mở
            _ = Task.Run(() => _tourDetailViewModel.WarmUpLocationPermissionAsync());

            _ = _apiService.SyncCoreDataFromServerIfOnlineAsync();
            _ = _apiService.SyncFavoritesIfOnlineAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[APP] Startup init failed: {ex}");
        }
    }

}