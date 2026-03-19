using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Devices.Sensors;
using System.Diagnostics;
using System.Collections.ObjectModel;
using TravelSystem.Mobile.Services;

namespace TravelSystem.Mobile.ViewModels;

public partial class TourDetailViewModel : ObservableObject, IQueryAttributable
{
    private readonly ApiService _apiService;
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private readonly SemaphoreSlim _permissionLock = new(1, 1);
    private CancellationTokenSource? _locationCts;
    private Location? _lastMapRefreshLocation;
    private bool _isForegroundTracking;
    private bool _hasCheckedLocationPermission;
    private bool _isLocationPermissionGranted;
    private bool _isLoading;

    // Cache vị trí tĩnh giữa các lần điều hướng để tránh GPS cold-start
    private static Location? _cachedUserLocation;
    private string _tourName = "Tour details";

    private const double GeofenceTriggerMeters = 80;
    private const double MapRefreshMoveThresholdMeters = 35;
    private static readonly TimeSpan ForegroundTrackingInterval = TimeSpan.FromSeconds(8);

    public ObservableCollection<PoiStopItem> PoiStops { get; } = [];
    public IAsyncRelayCommand LoadDataCommand { get; }
    public IAsyncRelayCommand<PoiStopItem> ToggleFavoriteCommand { get; }
    public IRelayCommand<PoiStopItem> SelectStopCommand { get; }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public string TourName
    {
        get => _tourName;
        set => SetProperty(ref _tourName, value);
    }

    public int TourId { get; private set; }
    public Location? UserLocation { get; private set; }

    public event EventHandler? MapDataChanged;

    private static void Trace(string message)
    {
        Debug.WriteLine($"[TOUR_DETAIL][{DateTime.Now:HH:mm:ss.fff}] {message}");
    }

    public TourDetailViewModel(ApiService apiService)
    {
        _apiService = apiService;
        LoadDataCommand = new AsyncRelayCommand(LoadData);
        ToggleFavoriteCommand = new AsyncRelayCommand<PoiStopItem>(ToggleFavorite);
        SelectStopCommand = new RelayCommand<PoiStopItem>(SelectStop);
    }

    /// <summary>
    /// Gọi sớm khi app khởi động (background) để cache kết quả quyền GPS
    /// trước khi user mở TourDetailPage.
    /// </summary>
    public async Task WarmUpLocationPermissionAsync()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await EnsureLocationPermissionAsync(cts.Token);
            Trace($"WarmUp: permission={_isLocationPermissionGranted}");
        }
        catch
        {
            // Không quan trọng nếu warm-up thất bại
        }
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        int newTourId = TourId;
        if (query.TryGetValue("tourId", out var tourIdObj) && int.TryParse(tourIdObj?.ToString(), out var id))
        {
            newTourId = id;
        }

        if (query.TryGetValue("tourName", out var tourNameObj))
        {
            TourName = Uri.UnescapeDataString(tourNameObj?.ToString() ?? string.Empty);
        }

        // Nếu chuyển sang tour khác → reset toàn bộ state liên quan tour cũ
        if (newTourId != TourId || PoiStops.Count == 0)
        {
            TourId = newTourId;
            PoiStops.Clear();
            IsLoading = false; // reset để LoadData không bị block bởi IsLoading guard
            _locationCts?.Cancel();
            _locationCts?.Dispose();
            _locationCts = null;
        }
        else
        {
            TourId = newTourId;
        }

        Trace($"ApplyQueryAttributes => TourId={TourId}, TourName='{TourName}'");

        _ = LoadData();
    }

    public async Task LoadData()
    {
        if (TourId <= 0) return;
        if (IsLoading) return;

        await _loadLock.WaitAsync();
        var loadingStartedAt = DateTime.UtcNow;
        var sw = Stopwatch.StartNew();

        try
        {
            Trace($"LoadData START (TourId={TourId})");
            IsLoading = true;

            var stopsTask = _apiService.GetTourStopsAsync(TourId);
            Trace("Requested local tour stops");

            var stops = await stopsTask;
            Trace($"Stops resolved: {(stops == null ? "null" : stops.Count.ToString())}");

            PoiStops.Clear();
            if (stops != null)
            {
                foreach (var stop in stops)
                {
                    PoiStops.Add(new PoiStopItem
                    {
                        ZoneId = stop.ZoneId,
                        OrderIndex = stop.OrderIndex,
                        Name = stop.Name,
                        Description = stop.Description ?? string.Empty,
                        ImageUrl = stop.ImageUrl ?? string.Empty,
                        Latitude = stop.Latitude,
                        Longitude = stop.Longitude,
                        IsFavorite = false
                    });
                }
            }

            if (PoiStops.Count > 0)
            {
                PoiStops[0].IsSelected = true;
                Trace($"Initial selected ZoneId={PoiStops[0].ZoneId}");
            }

            MapDataChanged?.Invoke(this, EventArgs.Empty);
            Trace("MapDataChanged invoked after loading stops");

            _locationCts?.Cancel();
            _locationCts?.Dispose();
            _locationCts = new CancellationTokenSource();
            var token = _locationCts.Token;
            _ = Task.Run(() => UpdateUserLocationAndDistance(token, forceMapRefresh: true));
        }
        finally
        {
            IsLoading = false;
            _loadLock.Release();
            sw.Stop();
            Trace($"LoadData END in {sw.ElapsedMilliseconds} ms");
        }
    }

    public void StartForegroundTracking()
    {
        if (_isForegroundTracking) return;
        _isForegroundTracking = true;

        _locationCts?.Cancel();
        _locationCts?.Dispose();
        _locationCts = new CancellationTokenSource();
        // Chạy trên background thread để tránh block main thread khi xin quyền GPS
        _ = Task.Run(() => ForegroundTrackingLoopAsync(_locationCts.Token));
    }

    public void StopForegroundTracking()
    {
        _isForegroundTracking = false;
        _locationCts?.Cancel();
        _locationCts?.Dispose();
        _locationCts = null;
    }

    private async Task ForegroundTrackingLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await UpdateUserLocationAndDistance(cancellationToken);

            try
            {
                await Task.Delay(ForegroundTrackingInterval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task ToggleFavorite(PoiStopItem stop)
    {
        if (stop == null) return;

        var guestId = await _apiService.EnsureGuestIdAsync();
        bool success;

        if (stop.IsFavorite)
        {
            success = await _apiService.RemoveFavoriteAsync(guestId, stop.ZoneId);
            if (success) stop.IsFavorite = false;
        }
        else
        {
            success = await _apiService.AddFavoriteAsync(guestId, stop.ZoneId);
            if (success) stop.IsFavorite = true;
        }
    }

    private void SelectStop(PoiStopItem stop)
    {
        if (stop == null) return;

        foreach (var poi in PoiStops)
        {
            poi.IsSelected = poi.ZoneId == stop.ZoneId;
        }

        MapDataChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SelectStopByZoneId(int zoneId)
    {
        var stop = PoiStops.FirstOrDefault(x => x.ZoneId == zoneId);
        if (stop != null)
        {
            SelectStop(stop);
        }
    }

    private async Task UpdateUserLocationAndDistance(CancellationToken cancellationToken, bool forceMapRefresh = false)
    {
        try
        {
            if (cancellationToken.IsCancellationRequested) return;

            var hasPermission = await EnsureLocationPermissionAsync(cancellationToken);
            if (!hasPermission)
            {
                return;
            }

            // Ưu tiên: cache tĩnh → LastKnown → GPS với timeout ngắn (1.5s, Low accuracy)
            UserLocation = _cachedUserLocation
                ?? await Geolocation.Default.GetLastKnownLocationAsync()
                ?? await Geolocation.Default.GetLocationAsync(
                    new GeolocationRequest(GeolocationAccuracy.Low, TimeSpan.FromSeconds(1.5)));

            if (UserLocation != null)
            {
                _cachedUserLocation = UserLocation;
            }

            if (UserLocation == null)
            {
                return;
            }

            if (cancellationToken.IsCancellationRequested) return;

            foreach (var stop in PoiStops)
            {
                var distanceKm = Location.CalculateDistance(
                    UserLocation,
                    new Location(stop.Latitude, stop.Longitude),
                    DistanceUnits.Kilometers);

                stop.DistanceText = $"{distanceKm:0.0} km";
            }

            var selectionChanged = TryAutoSelectNearestStop(UserLocation);
            var shouldRefreshMap = forceMapRefresh || selectionChanged || ShouldRefreshMapByMovement(UserLocation);

            if (shouldRefreshMap)
            {
                _lastMapRefreshLocation = UserLocation;
                MapDataChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        catch
        {
            // Keep default "--" if location unavailable.
        }
    }

    private async Task<bool> EnsureLocationPermissionAsync(CancellationToken cancellationToken)
    {
        if (_hasCheckedLocationPermission)
        {
            return _isLocationPermissionGranted;
        }

        await _permissionLock.WaitAsync(cancellationToken);
        try
        {
            if (_hasCheckedLocationPermission)
            {
                return _isLocationPermissionGranted;
            }

            var permission = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (permission != PermissionStatus.Granted)
            {
                permission = await MainThread.InvokeOnMainThreadAsync(Permissions.RequestAsync<Permissions.LocationWhenInUse>);
            }

            _isLocationPermissionGranted = permission == PermissionStatus.Granted;
            _hasCheckedLocationPermission = true;
            return _isLocationPermissionGranted;
        }
        finally
        {
            _permissionLock.Release();
        }
    }

    private bool TryAutoSelectNearestStop(Location userLocation)
    {
        if (PoiStops.Count == 0)
        {
            return false;
        }

        PoiStopItem? nearestStop = null;
        double nearestDistanceMeters = double.MaxValue;

        foreach (var stop in PoiStops)
        {
            var distanceMeters = Location.CalculateDistance(
                userLocation,
                new Location(stop.Latitude, stop.Longitude),
                DistanceUnits.Kilometers) * 1000d;

            if (distanceMeters < nearestDistanceMeters)
            {
                nearestDistanceMeters = distanceMeters;
                nearestStop = stop;
            }
        }

        if (nearestStop == null || nearestDistanceMeters > GeofenceTriggerMeters)
        {
            return false;
        }

        var currentSelected = PoiStops.FirstOrDefault(x => x.IsSelected);
        if (currentSelected?.ZoneId == nearestStop.ZoneId)
        {
            return false;
        }

        foreach (var stop in PoiStops)
        {
            stop.IsSelected = stop.ZoneId == nearestStop.ZoneId;
        }

        return true;
    }

    private bool ShouldRefreshMapByMovement(Location userLocation)
    {
        if (_lastMapRefreshLocation == null)
        {
            return true;
        }

        var movedMeters = Location.CalculateDistance(_lastMapRefreshLocation, userLocation, DistanceUnits.Kilometers) * 1000d;
        return movedMeters >= MapRefreshMoveThresholdMeters;
    }
}

public class PoiStopItem : ObservableObject
{
    private bool _isSelected;
    private bool _isFavorite;
    private string _distanceText = "--";

    public int ZoneId { get; init; }
    public int OrderIndex { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string ImageUrl { get; init; } = string.Empty;
    public double Latitude { get; init; }
    public double Longitude { get; init; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public bool IsFavorite
    {
        get => _isFavorite;
        set => SetProperty(ref _isFavorite, value);
    }

    public string DistanceText
    {
        get => _distanceText;
        set => SetProperty(ref _distanceText, value);
    }
}
