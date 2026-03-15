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
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
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
		builder.Services.AddSingleton<MainPage>();
		builder.Services.AddSingleton<AppShell>();
        builder.Services.AddTransient<LanguageSelectionPage>();

		return builder.Build();
	}
}
