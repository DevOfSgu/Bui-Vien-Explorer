using System;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using TravelSystem.Mobile.Services;
using TravelSystem.Mobile.ViewModels;
using System.Threading.Tasks;

namespace TravelSystem.Mobile;

public partial class App : Application
{
    private readonly DatabaseService _dbService;
    private readonly ApiService _apiService;
    private readonly TourDetailViewModel _tourDetailViewModel;
    private readonly AudioPreloadService _audioPreloadService;
    private readonly IAppAudioInterruptionService _audioInterruptionService;
    private readonly LocalizationManager _localizationManager = LocalizationManager.Instance;

    public App(DatabaseService dbService, ApiService apiService, TourDetailViewModel tourDetailViewModel, AudioPreloadService audioPreloadService, IAppAudioInterruptionService audioInterruptionService)
    {
        RegisterGlobalExceptionHandlers();
        InitializeComponent();
        _dbService = dbService;
        _apiService = apiService;
        _tourDetailViewModel = tourDetailViewModel;
        _audioPreloadService = audioPreloadService;
        _audioInterruptionService = audioInterruptionService;
        
        // Mặc định là Light mode
        UserAppTheme = AppTheme.Light;
        InitializeTheme();
        _ = InitializeLanguageAsync();
        
        Debug.WriteLine("✅ App initialized successfully");
    }

    private static void RegisterGlobalExceptionHandlers()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            Debug.WriteLine($"[FATAL][AppDomain] {args.ExceptionObject}");
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Debug.WriteLine($"[FATAL][TaskScheduler] {args.Exception}");
            args.SetObserved();
        };
    }

    protected override void OnSleep()
    {
        base.OnSleep();
        _audioInterruptionService.BeginInterruption();
    }

    protected override void OnResume()
    {
        base.OnResume();
        _audioInterruptionService.EndInterruption();
    }

    private async void InitializeTheme()
    {
        var isDarkStr = await _dbService.GetSettingAsync("IsDarkMode", "false");
        bool isDark = bool.Parse(isDarkStr);
        UserAppTheme = isDark ? AppTheme.Dark : AppTheme.Light;
    }

    private async Task InitializeLanguageAsync()
    {
        var langCode = await _dbService.GetSettingAsync("Language", "vi");
        _localizationManager.SetLanguage(langCode);
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
            {
                await _apiService.SyncAnalyticsIfOnlineAsync();
                await _apiService.SyncFavoritesIfOnlineAsync();
                _ = _audioPreloadService.StartPreloadAsync(); // Trigger preload on reconnect
            }
        };
    }

    private async Task InitializeAppAsync()
    {
        try
        {
            Debug.WriteLine("[APP] 🚀 Startup sequence started");

            // 1. Khởi tạo DB đầu tiên (tác vụ thiết yếu)
            await _dbService.InitializeAsync();
            Debug.WriteLine("[APP] ✅ Database ready");

            // 2. Các tác vụ nặng hoặc phụ thuộc mạng thì đẩy vào Task.Run
            // để Main Thread (UI) được giải phóng sớm nhất có thể
            _ = Task.Run(async () =>
            {
                try
                {
                    // A. Xử lý Session / Anonymous Install
                    await InitializeSessionAsync();
                    
                    // B. Giải nén Map (Nặng I/O)
                    Debug.WriteLine("[APP] 🗺️ Extracting Map...");
                    await MapHelper.EnsureOfflineMapExistsAsync();
                    Debug.WriteLine("[APP] ✅ Map ready");
                    
                    // C. Sync dữ liệu từ Server (Phụ thuộc mạng)
                    Debug.WriteLine("[APP] 🔄 Starting Core Sync...");
                    await _apiService.SyncCoreDataFromServerIfOnlineAsync();
                    await _apiService.SyncAnalyticsIfOnlineAsync();
                    await _apiService.SyncFavoritesIfOnlineAsync();
                    
                    // D. Tải trước audio (Ngầm)
                    Debug.WriteLine("[APP] 🎵 Starting Audio Preload...");
                    await _audioPreloadService.StartPreloadAsync();
                    
                    Debug.WriteLine("[APP] ✅ Startup tasks finished");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[APP] ❌ Background initialization error: {ex.Message}");
                }
            });

            // 3. Pre-warm GPS (Nền)
            _ = Task.Run(() => _tourDetailViewModel.WarmUpLocationPermissionAsync());
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[APP] 🔴 Critical startup failure: {ex.Message}");
        }
    }

    private async Task InitializeSessionAsync()
    {
        try
        {
            // Single source of truth for identity + install sync to avoid duplicate AppInstall events.
            await _apiService.EnsureAnonymousInstallSyncedAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[APP] Session init error: {ex.Message}");
        }
    }

}
