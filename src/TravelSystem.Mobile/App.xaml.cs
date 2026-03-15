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
        return new Window(new AppShell());
    }

    protected override async void OnStart()
    {
        base.OnStart();
        // 1. Chờ khởi tạo Database (Tạo bảng SQLite) xong
        await _dbService.InitializeAsync();
    }
}