using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using TravelSystem.Mobile.Services;

namespace TravelSystem.Mobile.ViewModels;

public partial class ZoneDetailViewModel : ObservableObject, IQueryAttributable
{
    private readonly ApiService _apiService;
    private readonly DatabaseService _dbService;
    private readonly LocalizationManager _localizationManager;

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

    public string DistanceAwayText => $"• {DistanceText} {_localizationManager["zone_away"]}";

    [ObservableProperty]
    private string _addressText = "--";

    [ObservableProperty]
    private string _hoursText = "--";

    public bool IsOpenNow => ComputeIsOpenNow(HoursText);
    public string OpenStatusText => _localizationManager[IsOpenNow ? "zone_open_now" : "zone_closed_now"];
    public string OpenStatusTextColor => IsOpenNow ? "#FF4B4B" : "#2563EB";
    public string OpenStatusBackgroundColor => IsOpenNow ? "#FFF1F2" : "#EFF6FF";

    [ObservableProperty]
    private bool _isFavorite;

    [ObservableProperty]
    private bool _isLoading;

    public IAsyncRelayCommand ToggleFavoriteCommand { get; }
    public IRelayCommand GoBackCommand { get; }
    public IAsyncRelayCommand GoHomeCommand { get; }

    public ZoneDetailViewModel(ApiService apiService, DatabaseService dbService)
    {
        _apiService = apiService;
        _dbService = dbService;
        _localizationManager = LocalizationManager.Instance;
        ToggleFavoriteCommand = new AsyncRelayCommand(ToggleFavorite);
        GoBackCommand = new RelayCommand(async () => await Shell.Current.GoToAsync(".."));
        GoHomeCommand = new AsyncRelayCommand(async () => await Shell.Current.GoToAsync("//MainPage"));
        _localizationManager.PropertyChanged += OnLocalizationChanged;
    }

    partial void OnDistanceTextChanged(string value)
    {
        OnPropertyChanged(nameof(DistanceAwayText));
    }

    partial void OnHoursTextChanged(string value)
    {
        RefreshOpenStatusBindings();
    }

    private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != "Item[]") return;
        OnPropertyChanged(nameof(DistanceAwayText));
        OnPropertyChanged(nameof(OpenStatusText));
    }

    private void RefreshOpenStatusBindings()
    {
        OnPropertyChanged(nameof(IsOpenNow));
        OnPropertyChanged(nameof(OpenStatusText));
        OnPropertyChanged(nameof(OpenStatusTextColor));
        OnPropertyChanged(nameof(OpenStatusBackgroundColor));
    }

    private static bool ComputeIsOpenNow(string? hoursText)
    {
        if (!TryParseHoursRange(hoursText, out var openTime, out var closeTime))
        {
            return false;
        }

        var now = DateTime.Now.TimeOfDay;
        if (closeTime <= openTime)
        {
            return now >= openTime || now < closeTime;
        }

        return now >= openTime && now < closeTime;
    }

    private static bool TryParseHoursRange(string? hoursText, out TimeSpan openTime, out TimeSpan closeTime)
    {
        openTime = default;
        closeTime = default;

        if (string.IsNullOrWhiteSpace(hoursText))
        {
            return false;
        }

        var parts = hoursText.Split('-', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            return false;
        }

        if (!TryParseTime(parts[0], out openTime))
        {
            return false;
        }

        if (!TryParseTime(parts[1], out closeTime))
        {
            return false;
        }

        return true;
    }

    private static bool TryParseTime(string? timeText, out TimeSpan time)
    {
        time = default;

        if (string.IsNullOrWhiteSpace(timeText))
        {
            return false;
        }

        var formats = new[] { "h:mm tt", "hh:mm tt", "H:mm", "HH:mm" };

        if (DateTime.TryParseExact(timeText.Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var dt))
        {
            time = dt.TimeOfDay;
            return true;
        }

        if (DateTime.TryParse(timeText.Trim(), CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out dt))
        {
            time = dt.TimeOfDay;
            return true;
        }

        return false;
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

        if (query.TryGetValue("address", out var address))
            AddressText = Uri.UnescapeDataString(address?.ToString() ?? AddressText);

        if (query.TryGetValue("hours", out var hours))
            HoursText = Uri.UnescapeDataString(hours?.ToString() ?? HoursText);
        
        Debug.WriteLine($"[ZONE_DETAIL] Loaded ZoneId={ZoneId}, Name={Name}");

        _ = RefreshZoneDetailAsync();
    }

    private async Task RefreshZoneDetailAsync()
    {
        if (ZoneId <= 0) return;

        try
        {
            var detail = await _apiService.GetZoneDetailAsync(ZoneId);
            if (detail == null) return;

            Name = string.IsNullOrWhiteSpace(detail.Name) ? Name : detail.Name;
            Description = string.IsNullOrWhiteSpace(detail.Description) ? Description : detail.Description;
            ImageUrl = string.IsNullOrWhiteSpace(detail.ImageUrl) ? ImageUrl : detail.ImageUrl;
            Latitude = detail.Latitude;
            Longitude = detail.Longitude;
            AddressText = string.IsNullOrWhiteSpace(detail.Address) ? "--" : detail.Address;
            HoursText = string.IsNullOrWhiteSpace(detail.Hours) ? "--" : detail.Hours;

            Debug.WriteLine($"[ZONE_DETAIL] Refreshed from API. ZoneId={ZoneId}, Hours={HoursText}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ZONE_DETAIL] RefreshZoneDetailAsync error: {ex.Message}");
        }
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
