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

    // Chuyển OnAppearing sang OnNavigated để đảm bảo Shell đã nạp đủ Component
    protected override async void OnNavigated(ShellNavigatedEventArgs args)
    {
        base.OnNavigated(args);

        // Chỉ kiểm tra onboarding ở lần đầu Shell load
        if (_isCheckedOnboarding) return;
        _isCheckedOnboarding = true;

        var dbService = IPlatformApplication.Current?.Services.GetService<DatabaseService>();
        if (dbService != null)
        {
            var lang = await dbService.GetSettingAsync("Language", "");
            // if (string.IsNullOrEmpty(lang))
            if (true)
            {
                // DispatchDelayed (hoãn 100ms) để nhường đường cho Animation của TabBar Android chạy xong
                // Tránh tranh chấp UI Thread gây lỗi JavaProxyThrowable
                Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(500), async () =>
                {
                    // Lỗi ở đây là xài // (Absolute Route) cho một trang không nằm trong TabBar. PHẢI dùng định tuyến Push tương đối!
                    await Shell.Current.GoToAsync($"{nameof(Views.LanguageSelectionPage)}");
                });
            }
        }
    }
}
