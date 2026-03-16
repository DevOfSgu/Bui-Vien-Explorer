using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Diagnostics;
using TravelSystem.Mobile.Services;

namespace TravelSystem.Mobile.ViewModels;

public partial class SavedPageViewModel : ObservableObject
{
    private readonly ApiService _apiService;

    public ObservableCollection<ZoneCardItem> FavoriteZones { get; } = [];

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _emptyStateMessage = "Chưa có địa điểm yêu thích nào.";

    public bool IsEmpty => !IsLoading && FavoriteZones.Count == 0;

    public SavedPageViewModel(ApiService apiService)
    {
        _apiService = apiService;
    }

    [RelayCommand]
    private async Task LoadFavorites()
    {
        try
        {
            IsLoading = true;
            FavoriteZones.Clear();

            var guestId = await _apiService.EnsureGuestIdAsync();
            var favorites = await _apiService.GetFavoritesAsync(guestId);

            if (favorites != null)
            {
                foreach (var fav in favorites)
                {
                    FavoriteZones.Add(new ZoneCardItem
                    {
                        Id = fav.ZoneId,
                        Name = fav.Zone.Name,
                        Description = fav.Zone.Description,
                        ImageUrl = string.Empty // API doesn't return ImageUrl for zone in join yet, or it's in FavoriteZoneDto
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SAVED_VM] Error: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(IsEmpty));
        }
    [RelayCommand]
    private async Task RemoveFavorite(ZoneCardItem item)
    {
        if (item == null) return;

        var guestId = await _apiService.EnsureGuestIdAsync();
        var success = await _apiService.RemoveFavoriteAsync(guestId, item.Id);
        
        if (success)
        {
            FavoriteZones.Remove(item);
            OnPropertyChanged(nameof(IsEmpty));
        }
    }
}

