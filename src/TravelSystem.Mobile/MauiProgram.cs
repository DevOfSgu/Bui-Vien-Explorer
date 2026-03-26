using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using SkiaSharp.Views.Maui.Controls.Hosting;
using TravelSystem.Mobile.Views;

namespace TravelSystem.Mobile;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
        SQLitePCL.Batteries_V2.Init();
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .UseMauiCommunityToolkitMediaElement(true)
            .UseSkiaSharp()
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
        builder.Services.AddSingleton<Services.IAudioGuideService, Services.AudioGuideService>();
        builder.Services.AddSingleton<Services.AudioPreloadService>();


		// Register ViewModels
		builder.Services.AddSingleton<ViewModels.MainPageViewModel>();
		builder.Services.AddSingleton<ViewModels.SavedPageViewModel>();
        builder.Services.AddSingleton<ViewModels.TourDetailViewModel>();
        builder.Services.AddTransient<ViewModels.ZoneDetailViewModel>();
		builder.Services.AddTransient<ViewModels.LanguageSelectionViewModel>();
		builder.Services.AddTransient<ViewModels.SettingsViewModel>();


		// Register Pages
		builder.Services.AddTransient<MainPage>();
		builder.Services.AddTransient<AppShell>();
        builder.Services.AddTransient<SavedPage>();
        builder.Services.AddSingleton<TourDetailPage>();
        builder.Services.AddTransient<ZoneDetailPage>();
		builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddTransient<LanguageSelectionPage>();

		return builder.Build();
	}
}
