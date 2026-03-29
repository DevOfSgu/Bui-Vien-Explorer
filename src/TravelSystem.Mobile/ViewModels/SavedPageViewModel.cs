using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using TravelSystem.Mobile.Services;
using TravelSystem.Shared.Models;

namespace TravelSystem.Mobile.ViewModels;

public partial class SavedPageViewModel : ObservableObject
{
    private readonly ApiService _apiService;
    private readonly DatabaseService _dbService;
    private readonly LocalizationManager _localizationManager;
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private readonly SemaphoreSlim _navigationLock = new(1, 1);

    public ObservableCollection<ZoneCardItem> FavoriteZones { get; } = [];

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _emptyStateMessage = string.Empty;

    public bool IsEmpty => !IsLoading && FavoriteZones.Count == 0;
    public string SavedNavTitle => _localizationManager["saved_nav_title"];
    public string SavedTitleText => _localizationManager["saved_title"];
    public string SavedSubtitleText => _localizationManager["saved_subtitle"];

    public SavedPageViewModel(ApiService apiService, DatabaseService dbService)
    {
        _apiService = apiService;
        _dbService = dbService;
        _localizationManager = LocalizationManager.Instance;
        _localizationManager.PropertyChanged += OnLocalizationChanged;
        EmptyStateMessage = _localizationManager["saved_empty"];
    }

    private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != "Item[]" && e.PropertyName != "Item" && e.PropertyName != string.Empty) return;
        EmptyStateMessage = _localizationManager["saved_empty"];
        OnPropertyChanged(nameof(SavedNavTitle));
        OnPropertyChanged(nameof(SavedTitleText));
        OnPropertyChanged(nameof(SavedSubtitleText));
    }

    [RelayCommand]
    private async Task LoadFavorites()
    {
        if (!await _loadLock.WaitAsync(0)) // ← nếu đang chạy thì bỏ qua
        {
            Debug.WriteLine("[SAVED_VM] LoadFavorites skipped — already loading");
            return;
        }
        try
        {
            IsLoading = true;
            FavoriteZones.Clear();

            await _dbService.InitializeAsync();
            var guestId = await _apiService.EnsureGuestIdAsync();
            Debug.WriteLine($"[SAVED_VM] LoadFavorites — guestId={guestId}");

            var localFavorites = await _dbService.GetLocalFavoritesAsync(guestId);
            Debug.WriteLine($"[SAVED_VM] Local DB returned {localFavorites.Count} favorites");

            if (localFavorites.Count == 0
                && Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
            {
                Debug.WriteLine("[SAVED_VM] Local empty + online → triggering sync...");
                await _apiService.SyncFavoritesIfOnlineAsync();
                localFavorites = await _dbService.GetLocalFavoritesAsync(guestId);
                Debug.WriteLine($"[SAVED_VM] After sync: {localFavorites.Count} favorites");
            }

            // Lấy zone info từ cache — không tạo request mới
            var zones = await _apiService.GetZonesAsync();
            var zoneMap = zones?.ToDictionary(z => z.Id) ?? [];
            Debug.WriteLine($"[SAVED_VM] Zone map loaded: {zoneMap.Count} zones");

            foreach (var fav in localFavorites)
            {
                Debug.WriteLine($"[SAVED_VM] → Id={fav.Id} ZoneId={fav.ZoneId} IsDeleted={fav.IsDeleted}");

                zoneMap.TryGetValue(fav.ZoneId, out var zone);
                Debug.WriteLine($"[SAVED_VM] Zone lookup ZoneId={fav.ZoneId} → {(zone != null ? zone.Name : "NOT FOUND")}");

                FavoriteZones.Add(new ZoneCardItem
                {
                    Id = fav.ZoneId,
                    Name = zone?.Name ?? $"Zone {fav.ZoneId}",
                    Description = zone?.Description ?? string.Empty,
                    ImageUrl = zone?.ImageUrl ?? string.Empty,
                    Latitude = zone is null ? 0 : Convert.ToDouble(zone.Latitude),
                    Longitude = zone is null ? 0 : Convert.ToDouble(zone.Longitude),
                    Radius = zone?.Radius ?? 0,
                    Address = "--",
                    Hours = "--",
                    IsFavorite = true
                });
            }

            Debug.WriteLine($"[SAVED_VM] Done — total UI items={FavoriteZones.Count}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SAVED_VM] ❌ Error: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(IsEmpty));
            _loadLock.Release();
        }
    }
    [RelayCommand]
    private async Task RemoveFavorite(ZoneCardItem item)
    {
        if (item == null) return;

        var guestId = await _apiService.EnsureGuestIdAsync();
        await _apiService.RemoveFavoriteAsync(guestId, item.Id);

        await LoadFavorites();
    }

    [RelayCommand]
    private async Task OpenZoneDetail(ZoneCardItem item)
    {
        if (item == null) return;
        if (!await _navigationLock.WaitAsync(0))
        {
            return;
        }

        try
        {
            var detail = await _apiService.GetZoneDetailAsync(item.Id);

            var zoneName = detail?.Name ?? item.Name;
            var zoneDescription = detail?.Description ?? item.Description;
            var zoneImage = detail?.ImageUrl ?? item.ImageUrl;
            var zoneLatitude = detail?.Latitude ?? item.Latitude;
            var zoneLongitude = detail?.Longitude ?? item.Longitude;
            var zoneAddress = detail?.Address ?? item.Address;
            var zoneHours = detail?.Hours ?? item.Hours;

            var parameters = new Dictionary<string, object>
            {
                { "zoneId", item.Id },
                { "name", Uri.EscapeDataString(zoneName) },
                { "description", Uri.EscapeDataString(zoneDescription ?? string.Empty) },
                { "imageUrl", Uri.EscapeDataString(zoneImage ?? string.Empty) },
                { "latitude", zoneLatitude },
                { "longitude", zoneLongitude },
                { "isFavorite", true },
                { "distance", Uri.EscapeDataString("--") },
                { "address", Uri.EscapeDataString(zoneAddress ?? "--") },
                { "hours", Uri.EscapeDataString(zoneHours ?? "--") }
            };

            await Shell.Current.GoToAsync(nameof(Views.ZoneDetailPage), parameters);
        }
        finally
        {
            _navigationLock.Release();
        }
    }
}

