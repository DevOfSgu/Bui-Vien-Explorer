using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using TravelSystem.Mobile.Services;
using TravelSystem.Shared.Models;

namespace TravelSystem.Mobile.ViewModels;

public partial class SavedPageViewModel : ObservableObject
{
    private readonly ApiService _apiService;
    private readonly DatabaseService _dbService;
    private readonly SemaphoreSlim _loadLock = new(1, 1);

    public ObservableCollection<ZoneCardItem> FavoriteZones { get; } = [];

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _emptyStateMessage = "Chưa có địa điểm yêu thích nào.";

    public bool IsEmpty => !IsLoading && FavoriteZones.Count == 0;

    public SavedPageViewModel(ApiService apiService, DatabaseService dbService)
    {
        _apiService = apiService;
        _dbService = dbService;
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
                    Description = zone?.Description ?? string.Empty
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
        var success = await _apiService.RemoveFavoriteAsync(guestId, item.Id);

        await LoadFavorites();
    }
}

