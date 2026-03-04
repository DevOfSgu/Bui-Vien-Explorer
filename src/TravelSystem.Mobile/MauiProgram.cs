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
		builder.Logging.AddDebug();
#endif
		// Register services
		builder.Services.AddSingleton<TravelSystem.Shared.Factories.SqliteConnectionFactory>();
        builder.Services.AddSingleton<Services.DatabaseService>();
		builder.Services.AddSingleton<MainPage>();
		builder.Services.AddSingleton<AppShell>();

		return builder.Build();
	}
}
