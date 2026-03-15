using Microsoft.Extensions.DependencyInjection;
using TravelSystem.Mobile.Services;

namespace TravelSystem.Mobile;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();
        
        // Đăng ký Route cho trang ngôn ngữ để dùng được GoToAsync
        Routing.RegisterRoute(nameof(Views.LanguageSelectionPage), typeof(Views.LanguageSelectionPage));
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
}
