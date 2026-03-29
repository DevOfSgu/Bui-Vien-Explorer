using Microsoft.Extensions.DependencyInjection;
using TravelSystem.Mobile.Services;
using System.ComponentModel;

namespace TravelSystem.Mobile;

public partial class AppShell : Shell
{
    private readonly LocalizationManager _localizationManager = LocalizationManager.Instance;

	public AppShell()
	{
		InitializeComponent();
        
        // Đăng ký Route cho trang ngôn ngữ để dùng được GoToAsync
        Routing.RegisterRoute(nameof(Views.LanguageSelectionPage), typeof(Views.LanguageSelectionPage));
        Routing.RegisterRoute(nameof(Views.TourDetailPage), typeof(Views.TourDetailPage));
        Routing.RegisterRoute(nameof(Views.ZoneDetailPage), typeof(Views.ZoneDetailPage));

        _localizationManager.PropertyChanged += OnLocalizationChanged;
        UpdateLocalizedShellText();
	}

	private bool _isCheckedOnboarding = false;

	protected override async void OnNavigated(ShellNavigatedEventArgs args)
	{
		base.OnNavigated(args);

		if (_isCheckedOnboarding) return;
		_isCheckedOnboarding = true;

		var dbService = IPlatformApplication.Current?.Services.GetService<DatabaseService>();
		if (dbService != null)
		{
			var lang = await dbService.GetSettingAsync("Language", "");
			if (string.IsNullOrEmpty(lang))
			{
				Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(500), async () =>
				{
					await Shell.Current.GoToAsync($"{nameof(Views.LanguageSelectionPage)}");
				});
			}
		}
	}

    private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != "Item[]" && e.PropertyName != "Item" && e.PropertyName != string.Empty)
            return;

        if (MainThread.IsMainThread)
        {
            UpdateLocalizedShellText();
            return;
        }

        MainThread.BeginInvokeOnMainThread(UpdateLocalizedShellText);
    }

    private void UpdateLocalizedShellText()
    {
        Title = _localizationManager["app_name"];

        if (HomeTab != null) HomeTab.Title = _localizationManager["tab_home"];
        if (SavedTab != null) SavedTab.Title = _localizationManager["tab_saved"];
        if (SettingsTab != null) SettingsTab.Title = _localizationManager["tab_settings"];
    }
}
