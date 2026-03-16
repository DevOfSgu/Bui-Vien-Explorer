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

    public ObservableCollection<ZoneCardItem> ZoneCards { get; } = [];

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _errorMessage = string.Empty;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public string ZonesAvailableText => $"{ZoneCards.Count} địa điểm có sẵn";

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

            var guestId = await _apiService.EnsureGuestIdAsync();
            Debug.WriteLine($"[MAIN_VM] Loading for Guest: {guestId}");

            var zones = await _apiService.GetZonesAsync();
            var favorites = await _apiService.GetFavoritesAsync(guestId);
            var favoriteIds = favorites?.Select(f => f.ZoneId).ToHashSet() ?? [];
            
            ZoneCards.Clear();
            if (zones != null && zones.Count > 0)
            {
                foreach (var zone in zones)
                {
                    ZoneCards.Add(new ZoneCardItem
                    {
                        Id = zone.Id,
                        Name = zone.Name ?? string.Empty,
                        Description = zone.Description ?? string.Empty,
                        ImageUrl = zone.ImageUrl ?? string.Empty,
                        IsFavorite = favoriteIds.Contains(zone.Id)
                    });
                }
            }
            else
            {
                ErrorMessage = "Không có địa điểm nào được tìm thấy.";
            }

            OnPropertyChanged(nameof(ZonesAvailableText));
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

    [RelayCommand]
    private async Task ToggleFavorite(ZoneCardItem item)
    {
        if (item == null) return;

        var guestId = await _apiService.EnsureGuestIdAsync();
        bool success;

        if (item.IsFavorite)
        {
            success = await _apiService.RemoveFavoriteAsync(guestId, item.Id);
            if (success) item.IsFavorite = false;
        }
        else
        {
            success = await _apiService.AddFavoriteAsync(guestId, item.Id);
            if (success) item.IsFavorite = true;
        }
    }

}

public partial class ZoneCardItem : ObservableObject
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string ImageUrl { get; init; } = string.Empty;
    public string IconGlyph { get; init; } = "📍";
    public Color IconBackgroundColor { get; init; } = Color.FromArgb("#FFE9E9");
    
    [ObservableProperty] private bool _isFavorite;

    // Fallback labels for UI
    public string StopsText => "1 điểm dừng";
    public string MinutesText => "8 phút";
}


