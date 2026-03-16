using Microsoft.Extensions.Logging;
using TravelSystem.Mobile.Views;

namespace TravelSystem.Mobile;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
                fonts.AddFont("SpaceGrotesk-Bold.ttf", "SpaceGroteskBold");
                fonts.AddFont("SpaceGrotesk-Light.ttf", "SpaceGroteskLight");
                fonts.AddFont("SpaceGrotesk-Medium.ttf", "SpaceGroteskMedium");
                fonts.AddFont("SpaceGrotesk-Regular.ttf", "SpaceGroteskRegular");
                fonts.AddFont("SpaceGrotesk-SemiBold.ttf", "SpaceGroteskSemiBold");
            });

#if DEBUG
     builder.Logging.SetMinimumLevel(LogLevel.Debug);
		builder.Logging.AddDebug();
#endif
		// Register services
		builder.Services.AddSingleton<TravelSystem.Shared.Factories.SqliteConnectionFactory>();
        builder.Services.AddSingleton<Services.DatabaseService>();
        builder.Services.AddSingleton<Services.ApiService>();

		// Register ViewModels
		builder.Services.AddSingleton<ViewModels.MainPageViewModel>();
		builder.Services.AddTransient<ViewModels.LanguageSelectionViewModel>();

		// Register Pages
		builder.Services.AddTransient<MainPage>();
		builder.Services.AddTransient<AppShell>();
        builder.Services.AddTransient<SavedPage>();
		builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddTransient<LanguageSelectionPage>();

		return builder.Build();
	}
}
