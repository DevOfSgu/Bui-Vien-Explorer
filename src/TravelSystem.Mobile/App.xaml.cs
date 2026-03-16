using System.Diagnostics;
using TravelSystem.Mobile.Services;

namespace TravelSystem.Mobile;

public partial class App : Application
{
    private readonly DatabaseService _dbService;
    private readonly ApiService _apiService;

    public App(DatabaseService dbService, ApiService apiService)
    {
        InitializeComponent();
        _dbService = dbService;
        _apiService = apiService;
        Debug.WriteLine("✅ App initialized successfully");
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new AppShell());
    }

    protected override async void OnStart()
    {
        base.OnStart();

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
    }
}