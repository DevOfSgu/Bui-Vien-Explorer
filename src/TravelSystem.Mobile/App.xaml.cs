using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using TravelSystem.Mobile.Services;

namespace TravelSystem.Mobile;

public partial class App : Application
{
    private readonly DatabaseService _dbService;
    public App(DatabaseService dbService)
	{
		InitializeComponent();
		_dbService = dbService;
		Debug.WriteLine("✅ App initialized successfully");
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
        var window = new Window(new AppShell());
        Debug.WriteLine("✅ Window created successfully");
        return window;
    }
    protected override async void OnStart()
    {
        base.OnStart();
        // Gọi tạo bảng ở đây sẽ an toàn hơn và không làm treo luồng chính
        await _dbService.InitializeAsync();
    }
}