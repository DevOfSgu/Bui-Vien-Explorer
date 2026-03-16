using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Graphics;
using System.Diagnostics;
using System.Collections.ObjectModel;
using TravelSystem.Mobile.Services;

namespace TravelSystem.Mobile.ViewModels;

public partial class MainPageViewModel : ObservableObject
{
    private readonly ApiService _apiService;
    private readonly DatabaseService _databaseService;

    public ObservableCollection<RouteCardItem> RouteCards { get; } = [];

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _errorMessage = string.Empty;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public string ToursAvailableText => $"{RouteCards.Count} tours available";

    partial void OnErrorMessageChanged(string value)
    {
        OnPropertyChanged(nameof(HasError));
    }

    public MainPageViewModel(ApiService apiService, DatabaseService databaseService)
    {
        _apiService = apiService;
        _databaseService = databaseService;
    }

    [RelayCommand]
    private async Task LoadData()
    {
        try
        {
            IsLoading = true;
            ErrorMessage = string.Empty;

            Debug.WriteLine("[MAIN_VM] Loading zones from server...");
            var zones = await _apiService.GetZonesAsync();
            
            RouteCards.Clear();
            if (zones != null && zones.Count > 0)
            {
                foreach (var zone in zones)
                {
                    RouteCards.Add(new RouteCardItem
                    {
                        Id = zone.Id,
                        Name = zone.Name ?? string.Empty,
                        Description = zone.Description ?? string.Empty,
                        StopCount = 1,
                        DurationMinutes = zone.ActiveTime > 0 ? zone.ActiveTime : 8,
                        ImageUrl = string.Empty
                    });
                }
                Debug.WriteLine($"[MAIN_VM] Loaded {zones.Count} zones from server.");
            }
            else
            {
                ErrorMessage = "Không có zone để hiển thị.";
            }

            OnPropertyChanged(nameof(ToursAvailableText));
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Không thể tải dữ liệu: {ex.Message}";
            Debug.WriteLine($"[MAIN_VM] Error: {ex}");
        }
        finally
        {
            IsLoading = false;
        }
    }

}

public sealed class RouteCardItem
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public int StopCount { get; set; }
    public int DurationMinutes { get; set; }
    public string ImageUrl { get; init; } = string.Empty;
    public string IconGlyph { get; init; } = "📍";
    public Color IconBackgroundColor { get; init; } = Color.FromArgb("#FFE9E9");
}
