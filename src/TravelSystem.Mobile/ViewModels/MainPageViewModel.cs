using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TravelSystem.Mobile.Services;

namespace TravelSystem.Mobile.ViewModels;

public partial class MainPageViewModel : ObservableObject
{
    private readonly ApiService _apiService;
    private int _count = 0;

    [ObservableProperty]
    private string _counterText = "Click me!";

    [ObservableProperty]
    private string _apiResult = "";

    public MainPageViewModel(ApiService apiService)
    {
        _apiService = apiService;
    }

    [RelayCommand]
    private void IncrementCounter()
    {
        _count++;
        CounterText = _count == 1
            ? $"Clicked {_count} time"
            : $"Clicked {_count} times";

        Microsoft.Maui.Accessibility.SemanticScreenReader.Announce(CounterText);
    }

    [RelayCommand]
    private async Task TestApi()
    {
        try
        {
            ApiResult = "Testing...";
            var result = await _apiService.TestConnectionAsync();
            ApiResult = $"✅ Success: {result}";
        }
        catch (Exception ex)
        {
            ApiResult = $"❌ Error: {ex.Message}";
        }
    }
}
