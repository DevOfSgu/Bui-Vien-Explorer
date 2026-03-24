using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Diagnostics;
using TravelSystem.Mobile.Services;

namespace TravelSystem.Mobile.ViewModels;

public partial class ZoneDetailViewModel : ObservableObject, IQueryAttributable
{
    private readonly ApiService _apiService;
    private readonly DatabaseService _dbService;

    [ObservableProperty]
    private int _zoneId;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _imageUrl = string.Empty;

    [ObservableProperty]
    private double _latitude;

    [ObservableProperty]
    private double _longitude;

    [ObservableProperty]
    private string _distanceText = "--";

    [ObservableProperty]
    private bool _isFavorite;

    [ObservableProperty]
    private bool _isLoading;

    public IAsyncRelayCommand ToggleFavoriteCommand { get; }
    public IRelayCommand GoBackCommand { get; }

    public ZoneDetailViewModel(ApiService apiService, DatabaseService dbService)
    {
        _apiService = apiService;
        _dbService = dbService;
        ToggleFavoriteCommand = new AsyncRelayCommand(ToggleFavorite);
        GoBackCommand = new RelayCommand(async () => await Shell.Current.GoToAsync(".."));
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("zoneId", out var zid) && int.TryParse(zid?.ToString(), out var id))
            ZoneId = id;
        
        if (query.TryGetValue("name", out var n))
            Name = Uri.UnescapeDataString(n?.ToString() ?? string.Empty);

        if (query.TryGetValue("description", out var desc))
            Description = Uri.UnescapeDataString(desc?.ToString() ?? string.Empty);

        if (query.TryGetValue("imageUrl", out var img))
            ImageUrl = Uri.UnescapeDataString(img?.ToString() ?? string.Empty);

        if (query.TryGetValue("latitude", out var lat) && double.TryParse(lat?.ToString(), out var lval))
            Latitude = lval;

        if (query.TryGetValue("longitude", out var lon) && double.TryParse(lon?.ToString(), out var lnval))
            Longitude = lnval;

        if (query.TryGetValue("isFavorite", out var fav) && bool.TryParse(fav?.ToString(), out var bval))
            IsFavorite = bval;

        if (query.TryGetValue("distance", out var dist))
            DistanceText = Uri.UnescapeDataString(dist?.ToString() ?? "--");
        
        Debug.WriteLine($"[ZONE_DETAIL] Loaded ZoneId={ZoneId}, Name={Name}");
    }

    private async Task ToggleFavorite()
    {
        try
        {
            var guestId = await _apiService.EnsureGuestIdAsync();
            bool success = false;

            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
            {
                if (IsFavorite)
                {
                    await _dbService.MarkLocalFavoriteDeletedAsync(guestId, ZoneId);
                    await _dbService.InsertPendingOpAsync(new TravelSystem.Shared.Models.PendingFavoriteOp
                    {
                        GuestId = guestId,
                        ZoneId = ZoneId,
                        Operation = TravelSystem.Shared.Models.FavoriteOperation.Remove,
                        CreatedAt = DateTime.UtcNow
                    });
                    IsFavorite = false;
                    success = true;
                }
                else
                {
                    await _dbService.InsertOrUpdateLocalFavoriteAsync(new TravelSystem.Shared.Models.LocalFavorite
                    {
                        GuestId = guestId,
                        ZoneId = ZoneId,
                        CreatedAt = DateTime.UtcNow,
                        IsDeleted = 0
                    });
                    await _dbService.InsertPendingOpAsync(new TravelSystem.Shared.Models.PendingFavoriteOp
                    {
                        GuestId = guestId,
                        ZoneId = ZoneId,
                        Operation = TravelSystem.Shared.Models.FavoriteOperation.Add,
                        CreatedAt = DateTime.UtcNow
                    });
                    IsFavorite = true;
                    success = true;
                }
            }
            else
            {
                if (IsFavorite)
                {
                    success = await _apiService.RemoveFavoriteAsync(guestId, ZoneId);
                    if (success)
                    {
                        IsFavorite = false;
                        await _dbService.MarkLocalFavoriteDeletedAsync(guestId, ZoneId);
                    }
                }
                else
                {
                    success = await _apiService.AddFavoriteAsync(guestId, ZoneId);
                    if (success)
                    {
                        IsFavorite = true;
                        await _dbService.InsertOrUpdateLocalFavoriteAsync(new TravelSystem.Shared.Models.LocalFavorite
                        {
                            GuestId = guestId,
                            ZoneId = ZoneId,
                            CreatedAt = DateTime.UtcNow,
                            IsDeleted = 0
                        });
                    }
                }
                _ = Task.Run(() => _apiService.SyncFavoritesIfOnlineAsync());
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ZONE_DETAIL] ToggleFavorite error: {ex.Message}");
        }
    }
}
