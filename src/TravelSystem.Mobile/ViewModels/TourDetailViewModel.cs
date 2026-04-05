using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Networking;
using System.Diagnostics;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using TravelSystem.Mobile.Services;

namespace TravelSystem.Mobile.ViewModels;

public partial class TourDetailViewModel : ObservableObject, IQueryAttributable
{
    private readonly ApiService _apiService;
    private readonly DatabaseService _dbService;
    private readonly LocalizationManager _localizationManager;
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private readonly SemaphoreSlim _permissionLock = new(1, 1);
    private readonly HashSet<int> _favoritePendingZoneIds = new();
    private readonly object _favoriteLock = new();
    private CancellationTokenSource? _locationCts;
    private Location? _lastMapRefreshLocation;
    private bool _isForegroundTracking;
    private bool _hasCheckedLocationPermission;
    private bool _isLocationPermissionGranted;
    private bool _isLoading;
    private readonly HashSet<int> _trackedEnterZones = [];
    private DateTime _lastLocationPingAtUtc = DateTime.MinValue;
    private int? _autoSelectCandidateZoneId;
    private DateTime _autoSelectCandidateSinceUtc = DateTime.MinValue;
    private DateTime _lastAutoSelectTriggeredAtUtc = DateTime.MinValue;
    private int? _lastAutoSelectTriggeredZoneId;
    private int? _currentAutoInsideZoneId;
    private int _currentZoneOutsideSamples;
    private int _candidateInsideSamples;
    private Location? _simulatedLocation;
    private Location? _stationaryAnchorLocation;
    private DateTime _stationaryCandidateSinceUtc = DateTime.MinValue;
    private DateTime _stationaryLockSuppressedUntilUtc = DateTime.MinValue;
    private bool _isStationaryLocked;
    private long _locationApplySeq;
    private long _mapRefreshSeq;
    private DateTime _lastLocationAppliedAtUtc = DateTime.MinValue;
    private DateTime _lastMapRefreshAtUtc = DateTime.MinValue;
    private readonly SemaphoreSlim _locationStreamLock = new(1, 1);
    private bool _isLocationListening;
    private Location? _latestStreamLocation;
    private DateTime _latestStreamLocationAtUtc = DateTime.MinValue;
    private DateTime _lastPollingAttemptAtUtc = DateTime.MinValue;
    private string _remoteExploreHintText = string.Empty;
    private bool _isRemoteExploreHintVisible;
    private bool _remoteExploreHintShownInCurrentFarSession;
    private CancellationTokenSource? _remoteHintAutoHideCts;

    // Cache vị trí tĩnh giữa các lần điều hướng để tránh GPS cold-start
    private static Location? _cachedUserLocation;
    private string _tourName = "Tour details";

    private const double GeofenceTriggerMeters = 45;
    private const double GeofenceExitHysteresisFactor = 1.25;
    private const double MinPracticalGeofenceMeters = 28;
    private const double MaxGpsAccuracyCompensationMeters = 35;
    private const double MaxEntryAccuracyCompensationMeters = 8;
    private const double EntryAccuracyCompensationEligibleMeters = 20;
    private const double AutoSwitchRequireDistanceMeters = 32;
    private const double FastAutoSelectDistanceMeters = 12;
    private const double FastAutoSelectMaxAccuracyMeters = 12;
    private const int CurrentZoneExitConfirmSamples = 2;
    private const int CandidateEnterConfirmSamples = 1;
    private const double MapRefreshMoveThresholdMeters = 2.5;
    private const double GpsJitterIgnoreMeters = 2.2;
    private const double GpsSmoothingThresholdMeters = 8;
    private const double GpsSmoothingAlpha = 0.75;
    private const double GoodAccuracySkipSmoothingMeters = 6;
    private const double MaxAcceptableGpsAccuracyMeters = 45;
    private const double PoorAccuracyRequireMoveMeters = 18;
    private const double StationaryEnterMoveMeters = 1.2;
    private const double StationaryReleaseMoveMeters = 9;
    private const double StationaryReleaseRawMoveMeters = 5;
    private const double StationaryReleaseSpeedMetersPerSecond = 1.2;
    private const double StationaryEnterMaxSpeedMetersPerSecond = 0.35;
    private const double StationaryEnterMaxAccuracyMeters = 25;
    private const double RawPoiAssistMaxAccuracyMeters = 30;
    private const double FallbackAutoSelectMaxAccuracyMeters = 20;
    private const double RouteSnapMaxDistanceMeters = 40;
    // Bắt buộc snap tọa độ vào tuyến đường bất chấp GPS báo accuracy tốt thế nào,
    // vỉ môi trường phố đi bộ Bùi Viện (chữ U) làm GPS bị phản xạ (ảo 1.3m accuracy nhưng vẫn trôi 30m).
    private const double RouteSnapSkipWhenAccuracyBetterThanMeters = 0.0;
    private const double BuiVienLatitude = 10.764017;
    private const double BuiVienLongitude = 106.692527;
    private const double RemoteExploreHintThresholdMeters = 1000;
    private static readonly TimeSpan ForegroundTrackingInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan MaxAcceptedLocationAge = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan StationaryLockDebounce = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan StationaryRelockCooldown = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan LocationPingInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan SimulationLocationPingInterval = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan AutoSelectDebounce = TimeSpan.FromSeconds(0.35);
    private static readonly TimeSpan AutoSelectSwitchDebounce = TimeSpan.FromSeconds(0.7);
    private static readonly TimeSpan FastAutoSelectDebounce = TimeSpan.FromSeconds(0.35);
    private static readonly TimeSpan AutoSelectCooldown = TimeSpan.FromSeconds(2.5);
    private static readonly TimeSpan FallbackAutoSelectMaxAge = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan StreamFreshWindow = TimeSpan.FromSeconds(2.6);
    private static readonly TimeSpan PollFallbackInterval = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan SimulationStepInterval = TimeSpan.FromMilliseconds(700);
    private static readonly TimeSpan SimulationPauseAtPoi = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan RemoteExploreHintDisplayDuration = TimeSpan.FromSeconds(4);

    public event EventHandler<PoiStopItem>? StopSelected;

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
    public DateTime LastLocationAppliedAtUtc => _lastLocationAppliedAtUtc;
    public DateTime LastMapRefreshAtUtc => _lastMapRefreshAtUtc;

    public event EventHandler? MapDataChanged;
    public string TourStopsTitle => _localizationManager["tour_stops"];
    public string StopsCountText => _localizationManager.Format("tour_stops_count", PoiStops.Count);
    public string PoiSimulationButtonText => IsPoiSimulationEnabled ? "Tắt giả lập POI" : "Bật giả lập POI";
    public string RemoteExploreHintText
    {
        get => _remoteExploreHintText;
        private set => SetProperty(ref _remoteExploreHintText, value);
    }

    public bool IsRemoteExploreHintVisible
    {
        get => _isRemoteExploreHintVisible;
        private set => SetProperty(ref _isRemoteExploreHintVisible, value);
    }

    private static void Trace(string message)
    {
        DiagnosticLogService.Log("TOUR_DETAIL", message);
    }

    public TourDetailViewModel(ApiService apiService, DatabaseService dbService)
    {
        _apiService = apiService;
        _dbService = dbService;
        _localizationManager = LocalizationManager.Instance;
        LoadDataCommand = new AsyncRelayCommand(LoadData);
        ToggleFavoriteCommand = new AsyncRelayCommand<PoiStopItem>(ToggleFavorite);
        SelectStopCommand = new RelayCommand<PoiStopItem>(stop => SelectStop(stop));
        NavigateToZoneDetailCommand = new AsyncRelayCommand<PoiStopItem>(NavigateToZoneDetail);

        PoiStops.CollectionChanged += OnPoiStopsCollectionChanged;
        _localizationManager.PropertyChanged += OnLocalizationChanged;
    }

    public IAsyncRelayCommand<PoiStopItem> NavigateToZoneDetailCommand { get; }
    private bool _isPoiSimulationEnabled;

    public bool IsPoiSimulationEnabled
    {
        get => _isPoiSimulationEnabled;
        set
        {
            if (!SetProperty(ref _isPoiSimulationEnabled, value)) return;
            OnPoiSimulationEnabledChanged(value);
        }
    }

    private void OnPoiStopsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(StopsCountText));
    }

    private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != "Item[]") return;
        OnPropertyChanged(nameof(TourStopsTitle));
        OnPropertyChanged(nameof(StopsCountText));
        UpdateRemoteExploreHint(UserLocation);
    }

    private async Task NavigateToZoneDetail(PoiStopItem? stop)
    {
        if (stop == null) return;

        SelectStop(stop, triggerEvent: false);

        var parameters = new Dictionary<string, object>
        {
            { "zoneId", stop.ZoneId },
            { "name", Uri.EscapeDataString(stop.Name) },
            { "description", Uri.EscapeDataString(stop.Description) },
            { "imageUrl", Uri.EscapeDataString(stop.ImageUrl) },
            { "latitude", stop.Latitude },
            { "longitude", stop.Longitude },
            { "isFavorite", stop.IsFavorite },
            { "distance", Uri.EscapeDataString(stop.DistanceText) },
            { "address", Uri.EscapeDataString(stop.Address) },
            { "hours", Uri.EscapeDataString(stop.Hours) }
        };

        if (_trackedEnterZones.Add(stop.ZoneId))
        {
            var lat = UserLocation?.Latitude ?? stop.Latitude;
            var lon = UserLocation?.Longitude ?? stop.Longitude;
            _ = _apiService.TrackEnterZoneAsync(stop.ZoneId, lat, lon);
        }

        await Shell.Current.GoToAsync(nameof(Views.ZoneDetailPage), parameters);
    }

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

        if (newTourId != TourId || PoiStops.Count == 0)
        {
            TourId = newTourId;
            PoiStops.Clear();
            _trackedEnterZones.Clear();
            ResetAutoSelectCandidate();
            _lastAutoSelectTriggeredAtUtc = DateTime.MinValue;
            _lastAutoSelectTriggeredZoneId = null;
            _currentAutoInsideZoneId = null;
            _simulatedLocation = null;
            ResetStationaryLock();
            CancelRemoteHintAutoHide();
            _remoteExploreHintShownInCurrentFarSession = false;
            RemoteExploreHintText = string.Empty;
            IsRemoteExploreHintVisible = false;
            IsLoading = false;
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
        if (TourId < 0) return;
        if (IsLoading) return;

        await _loadLock.WaitAsync();
        var sw = Stopwatch.StartNew();

        try
        {
            Trace($"LoadData START (TourId={TourId})");
            IsLoading = true;

            var stopsTask = _apiService.GetTourStopsAsync(TourId);
            var guestIdTask = _apiService.EnsureGuestIdAsync();
            Trace("Requested local tour stops");

            var stops = await stopsTask;
            Trace($"Stops resolved: {(stops == null ? "null" : stops.Count.ToString())}");

            var favoriteZoneIds = new HashSet<int>();
            try
            {
                var guestId = await guestIdTask;
                var remoteFavorites = await _apiService.GetFavoritesAsync(guestId);
                if (remoteFavorites != null)
                {
                    foreach (var f in remoteFavorites) favoriteZoneIds.Add(f.ZoneId);
                }

                var localFavorites = await _dbService.GetLocalFavoritesAsync(guestId);
                if (localFavorites != null)
                {
                    foreach (var f in localFavorites)
                    {
                        if (f.IsDeleted == 0)
                            favoriteZoneIds.Add(f.ZoneId);
                        else
                            favoriteZoneIds.Remove(f.ZoneId);
                    }
                }

                Trace($"Favorites resolved: Total={favoriteZoneIds.Count}");
            }
            catch (Exception ex)
            {
                Trace($"Favorites resolve failed: {ex.Message}");
            }

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
                        Radius = stop.Radius,
                        IsMain = stop.IsMain,
                        Address = stop.Address ?? "--",
                        Hours = stop.Hours ?? "--",
                        IsFavorite = favoriteZoneIds.Contains(stop.ZoneId)
                    });
                }
            }

            MapDataChanged?.Invoke(this, EventArgs.Empty);
            Trace("MapDataChanged invoked after loading stops");

            _locationCts?.Cancel();
            _locationCts?.Dispose();
            _locationCts = new CancellationTokenSource();
            var token = _locationCts.Token;

            if (_isForegroundTracking)
            {
                _ = Task.Run(() => ForegroundTrackingLoopAsync(token));
            }
            else
            {
                _ = Task.Run(() => UpdateUserLocationAndDistance(token, forceMapRefresh: true));
            }
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
        _ = Task.Run(() => ForegroundTrackingLoopAsync(_locationCts.Token));
        _ = Task.Run(() => EnsureLocationListeningAsync(_locationCts.Token));
    }

    public void StopForegroundTracking()
    {
        _isForegroundTracking = false;
        _locationCts?.Cancel();
        _locationCts?.Dispose();
        _locationCts = null;
        _ = Task.Run(StopLocationListeningAsync);
    }

    private async Task ForegroundTrackingLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (IsPoiSimulationEnabled)
            {
                await SimulatePoiMovementCycleAsync(cancellationToken);
            }
            else
            {
                await EnsureLocationListeningAsync(cancellationToken);
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
    }

    private async Task EnsureLocationListeningAsync(CancellationToken cancellationToken)
    {
        if (IsPoiSimulationEnabled) return;

        await _locationStreamLock.WaitAsync(cancellationToken);
        try
        {
            if (_isLocationListening) return;

            Geolocation.Default.LocationChanged -= OnLocationChanged;
            Geolocation.Default.LocationChanged += OnLocationChanged;

            var request = new GeolocationListeningRequest(GeolocationAccuracy.Best, TimeSpan.FromSeconds(2));
            var started = await Geolocation.Default.StartListeningForegroundAsync(request);
            _isLocationListening = started;
            Trace($"LOCATION_STREAM started={started}");
        }
        catch (Exception ex)
        {
            _isLocationListening = false;
            Trace($"LOCATION_STREAM start failed: {ex.Message}");
        }
        finally
        {
            _locationStreamLock.Release();
        }
    }

    private async Task StopLocationListeningAsync()
    {
        await _locationStreamLock.WaitAsync();
        try
        {
            Geolocation.Default.LocationChanged -= OnLocationChanged;
            if (_isLocationListening)
            {
                Geolocation.Default.StopListeningForeground();
            }
            _isLocationListening = false;
        }
        catch
        {
        }
        finally
        {
            _locationStreamLock.Release();
        }
    }

    private void OnLocationChanged(object? sender, GeolocationLocationChangedEventArgs e)
    {
        var location = e.Location;
        if (location == null) return;

        // [LOG] Ghi nhận từng update từ stream để phát hiện stream drop
        var acc = GetLocationAccuracyMeters(location);
        Trace(
            $"STREAM_UPDATE lat={location.Latitude:F6} lon={location.Longitude:F6} " +
            $"acc={(acc == double.MaxValue ? "unknown" : $"{acc:0.0}m")} " +
            $"speed={location.Speed?.ToString("0.00") ?? "n/a"}m/s");

        _latestStreamLocation = location;
        _latestStreamLocationAtUtc = DateTime.UtcNow;
    }

    private void OnPoiSimulationEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(PoiSimulationButtonText));
        ResetAutoSelectCandidate();
        _currentAutoInsideZoneId = null;
        _lastAutoSelectTriggeredZoneId = null;
        _lastAutoSelectTriggeredAtUtc = DateTime.MinValue;
        ResetStationaryLock();

        if (!value)
        {
            _simulatedLocation = null;
        }
    }

    [RelayCommand]
    private void TogglePoiSimulation()
    {
        IsPoiSimulationEnabled = !IsPoiSimulationEnabled;
        Trace($"POI simulation mode: {(IsPoiSimulationEnabled ? "ON" : "OFF")}");
    }

    private async Task SimulatePoiMovementCycleAsync(CancellationToken cancellationToken)
    {
        var orderedStops = PoiStops.OrderBy(s => s.OrderIndex).ToList();
        if (orderedStops.Count == 0)
        {
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            return;
        }

        _simulatedLocation ??= UserLocation ?? new Location(orderedStops[0].Latitude, orderedStops[0].Longitude);

        foreach (var stop in orderedStops)
        {
            if (cancellationToken.IsCancellationRequested || !IsPoiSimulationEnabled) return;

            await MoveSimulatedLocationToStopAsync(stop, cancellationToken);
            if (cancellationToken.IsCancellationRequested || !IsPoiSimulationEnabled) return;

            ForceSelectStopForSimulation(stop);

            var holdUntil = DateTime.UtcNow + SimulationPauseAtPoi;
            while (DateTime.UtcNow < holdUntil)
            {
                if (cancellationToken.IsCancellationRequested || !IsPoiSimulationEnabled) return;
                await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
            }
        }
    }

    private async Task MoveSimulatedLocationToStopAsync(PoiStopItem stop, CancellationToken cancellationToken)
    {
        var start = _simulatedLocation ?? UserLocation ?? new Location(stop.Latitude, stop.Longitude);
        var target = new Location(stop.Latitude, stop.Longitude);
        var distanceMeters = Location.CalculateDistance(start, target, DistanceUnits.Kilometers) * 1000d;
        var steps = (int)Math.Clamp(Math.Ceiling(distanceMeters / 12d), 6, 40);

        for (var step = 1; step <= steps; step++)
        {
            if (cancellationToken.IsCancellationRequested || !IsPoiSimulationEnabled) return;

            var t = step / (double)steps;
            var lat = start.Latitude + ((target.Latitude - start.Latitude) * t);
            var lon = start.Longitude + ((target.Longitude - start.Longitude) * t);

            _simulatedLocation = new Location(lat, lon);
            ApplyLocationAndRefresh(_simulatedLocation, rawLocationForPoi: null, forceMapRefresh: false, allowAutoSelect: false);

            await Task.Delay(SimulationStepInterval, cancellationToken);
        }

        _simulatedLocation = target;
        ApplyLocationAndRefresh(target, rawLocationForPoi: null, forceMapRefresh: false, allowAutoSelect: false);
    }

    private void ForceSelectStopForSimulation(PoiStopItem stop)
    {
        if (PoiStops.FirstOrDefault(x => x.IsSelected)?.ZoneId != stop.ZoneId)
        {
            SelectStop(stop, true);
        }

        _currentAutoInsideZoneId = stop.ZoneId;
        ResetAutoSelectCandidate();

        _ = _apiService.TrackEnterZoneAsync(stop.ZoneId, stop.Latitude, stop.Longitude);
    }

    private async Task ToggleFavorite(PoiStopItem? stop)
    {
        if (stop == null) return;

        lock (_favoriteLock)
        {
            if (!_favoritePendingZoneIds.Add(stop.ZoneId))
            {
                return;
            }
        }
        try
        {
            var guestId = await _apiService.EnsureGuestIdAsync();
            bool success = false;
            var before = stop.IsFavorite;

            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
            {
                if (stop.IsFavorite)
                {
                    await _dbService.MarkLocalFavoriteDeletedAsync(guestId, stop.ZoneId);
                    await _dbService.InsertPendingOpAsync(new TravelSystem.Shared.Models.PendingFavoriteOp
                    {
                        GuestId = guestId,
                        ZoneId = stop.ZoneId,
                        Operation = TravelSystem.Shared.Models.FavoriteOperation.Remove,
                        CreatedAt = DateTime.UtcNow
                    });
                    stop.IsFavorite = false;
                    success = true;
                }
                else
                {
                    await _dbService.InsertOrUpdateLocalFavoriteAsync(new TravelSystem.Shared.Models.LocalFavorite
                    {
                        GuestId = guestId,
                        ZoneId = stop.ZoneId,
                        CreatedAt = DateTime.UtcNow,
                        IsDeleted = 0
                    });
                    await _dbService.InsertPendingOpAsync(new TravelSystem.Shared.Models.PendingFavoriteOp
                    {
                        GuestId = guestId,
                        ZoneId = stop.ZoneId,
                        Operation = TravelSystem.Shared.Models.FavoriteOperation.Add,
                        CreatedAt = DateTime.UtcNow
                    });
                    stop.IsFavorite = true;
                    success = true;
                }
            }
            else
            {
                if (stop.IsFavorite)
                {
                    success = await _apiService.RemoveFavoriteAsync(guestId, stop.ZoneId);
                    if (success)
                    {
                        stop.IsFavorite = false;
                        await _dbService.MarkLocalFavoriteDeletedAsync(guestId, stop.ZoneId);
                    }
                }
                else
                {
                    success = await _apiService.AddFavoriteAsync(guestId, stop.ZoneId);
                    if (success)
                    {
                        stop.IsFavorite = true;
                        await _dbService.InsertOrUpdateLocalFavoriteAsync(new TravelSystem.Shared.Models.LocalFavorite
                        {
                            GuestId = guestId,
                            ZoneId = stop.ZoneId,
                            CreatedAt = DateTime.UtcNow,
                            IsDeleted = 0
                        });
                    }
                }

                _ = Task.Run(() => _apiService.SyncFavoritesIfOnlineAsync());
            }

            if (!success)
            {
                Trace($"ToggleFavorite failed for ZoneId={stop.ZoneId} (perhaps it's a marker only)");
                stop.IsFavorite = !before;
                await _dbService.InsertOrUpdateLocalFavoriteAsync(new TravelSystem.Shared.Models.LocalFavorite
                {
                    GuestId = guestId,
                    ZoneId = stop.ZoneId,
                    CreatedAt = DateTime.UtcNow,
                    IsDeleted = stop.IsFavorite ? 0 : 1
                });
                return;
            }

            Trace($"ToggleFavorite success ZoneId={stop.ZoneId}, before={before}, after={stop.IsFavorite}");
        }
        finally
        {
            lock (_favoriteLock)
            {
                _favoritePendingZoneIds.Remove(stop.ZoneId);
            }
        }
    }

    private void SelectStop(PoiStopItem? stop, bool triggerEvent = true, bool fromAutoSelect = false)
    {
        if (stop == null) return;

        var previousSelectedZoneId = PoiStops.FirstOrDefault(x => x.IsSelected)?.ZoneId;

        foreach (var poi in PoiStops)
        {
            poi.IsSelected = poi.ZoneId == stop.ZoneId;
        }

        var currentIndex = PoiStops.IndexOf(stop);
        if (currentIndex > 0)
        {
            PoiStops.Move(currentIndex, 0);
        }

        MapDataChanged?.Invoke(this, EventArgs.Empty);

        if (triggerEvent)
        {
            StopSelected?.Invoke(this, stop);
        }

        Trace(
            $"SELECT_STOP source={(fromAutoSelect ? "auto" : "manual")} zone={stop.ZoneId} " +
            $"prev={(previousSelectedZoneId?.ToString() ?? "none")} triggerAudio={triggerEvent}");

        if (!fromAutoSelect)
        {
            _currentAutoInsideZoneId = stop.ZoneId;
            _currentZoneOutsideSamples = 0;
            _lastAutoSelectTriggeredAtUtc = DateTime.UtcNow;
            _lastAutoSelectTriggeredZoneId = stop.ZoneId;
            ResetAutoSelectCandidate();
            Trace($"MANUAL_SELECT zone={stop.ZoneId} suppressAuto=0s");
        }
    }

    public void SelectStopByZoneId(int zoneId)
    {
        var stop = PoiStops.FirstOrDefault(x => x.ZoneId == zoneId);
        if (stop != null)
        {
            SelectStop(stop);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GPS CYCLE — FIX-2: Đơn giản hoá poll, bỏ double-attempt High→Medium
    // ─────────────────────────────────────────────────────────────────────────
    private async Task UpdateUserLocationAndDistance(CancellationToken cancellationToken, bool forceMapRefresh = false)
    {
        var cycleSw = Stopwatch.StartNew();
        try
        {
            if (cancellationToken.IsCancellationRequested) return;

            var hasPermission = await EnsureLocationPermissionAsync(cancellationToken);
            if (!hasPermission)
            {
                return;
            }

            var gpsFetchSw = Stopwatch.StartNew();
            var nowUtc = DateTime.UtcNow;
            var streamLocation = _latestStreamLocation;
            var streamAge = _latestStreamLocationAtUtc == DateTime.MinValue
                ? TimeSpan.MaxValue
                : nowUtc - _latestStreamLocationAtUtc;

            Location? freshLocation = null;
            var source = "stream";

            if (streamLocation != null && streamAge <= StreamFreshWindow)
            {
                // Stream còn tươi — dùng ngay, không poll
                freshLocation = streamLocation;
                Trace($"GPS_SOURCE stream age={streamAge.TotalSeconds:0.0}s");
            }
            else if (_lastPollingAttemptAtUtc == DateTime.MinValue || nowUtc - _lastPollingAttemptAtUtc >= PollFallbackInterval)
            {
                // [FIX-2] Chỉ dùng 1 lần poll với accuracy phù hợp:
                //   - Khi stream đang listen nhưng stale → Medium đủ dùng, timeout ngắn
                //   - Khi stream không listen (cold start) → High accuracy, timeout dài hơn
                source = "poll";
                _lastPollingAttemptAtUtc = nowUtc;

                var (accuracy, timeout) = _isLocationListening
                    ? (GeolocationAccuracy.Medium, TimeSpan.FromSeconds(1.0))   // stream backup
                    : (GeolocationAccuracy.High,   TimeSpan.FromSeconds(2.0));  // cold start

                Trace($"GPS_POLL attempt accuracy={accuracy} timeout={timeout.TotalSeconds:0.0}s streamListening={_isLocationListening} streamAge={(streamAge == TimeSpan.MaxValue ? "never" : $"{streamAge.TotalSeconds:0.0}s")}");

                freshLocation = await Geolocation.Default.GetLocationAsync(
                    new GeolocationRequest(accuracy, timeout),
                    cancellationToken);

                // [LOG] Kết quả poll để phát hiện poll fail pattern
                if (freshLocation == null)
                {
                    Trace($"GPS_POLL result=null — sẽ dùng lastKnown/cache fetchMs={gpsFetchSw.ElapsedMilliseconds}");
                }
                else
                {
                    var pollAcc = GetLocationAccuracyMeters(freshLocation);
                    Trace($"GPS_POLL result=ok acc={(pollAcc == double.MaxValue ? "unknown" : $"{pollAcc:0.0}m")} fetchMs={gpsFetchSw.ElapsedMilliseconds}");
                }
            }
            else
            {
                source = "stream-stale-no-poll";
                var timeSincePoll = nowUtc - _lastPollingAttemptAtUtc;
                Trace($"GPS_SOURCE stream-stale-no-poll streamAge={streamAge.TotalSeconds:0.0}s nextPollIn={(PollFallbackInterval - timeSincePoll).TotalSeconds:0.0}s");
            }

            var gpsFetchMs = gpsFetchSw.ElapsedMilliseconds;

            var rawLocation = freshLocation
                ?? await Geolocation.Default.GetLastKnownLocationAsync()
                ?? _cachedUserLocation;

            if (rawLocation == null)
            {
                Trace($"GPS cycle skipped: no location source (elapsed={cycleSw.ElapsedMilliseconds}ms)");
                return;
            }

            var hasFreshFixForAutoSelect = freshLocation != null && !IsLocationStale(freshLocation);
            if (freshLocation == null)
            {
                source = "fallback-lastKnown/cache";
            }

            // [FIX-2] Phân biệt rõ accuracy=unknown (null) khỏi accuracy=tốt
            var rawAccuracy = GetLocationAccuracyMeters(rawLocation);
            var rawAccuracyIsUnknown = rawAccuracy == double.MaxValue;
            var rawAccuracyDisplay = rawAccuracyIsUnknown ? "unknown" : $"{rawAccuracy:0.0}m";

            var rawAgeSeconds = rawLocation.Timestamp == default
                ? -1
                : (DateTimeOffset.UtcNow - rawLocation.Timestamp).TotalSeconds;

            // [LOG] Cảnh báo khi dùng location có accuracy unknown — dễ phát hiện cache stale
            if (rawAccuracyIsUnknown)
            {
                Trace($"GPS_WARN accuracy=unknown source={source} rawAge={(rawAgeSeconds < 0 ? "n/a" : $"{rawAgeSeconds:0.0}s")} — location này thiếu metadata accuracy");
            }

            if (!hasFreshFixForAutoSelect
                && freshLocation == null
                && rawAgeSeconds >= 0
                && rawAgeSeconds <= FallbackAutoSelectMaxAge.TotalSeconds
                && !rawAccuracyIsUnknown              // [FIX-2] Không dùng location không có accuracy cho fallback auto-select
                && rawAccuracy <= FallbackAutoSelectMaxAccuracyMeters)
            {
                hasFreshFixForAutoSelect = true;
                source += "+fallback-fresh";
            }

            if (IsLocationStale(rawLocation) && _cachedUserLocation != null)
            {
                rawLocation = _cachedUserLocation;
                hasFreshFixForAutoSelect = false;
                source = "cache-because-stale";
            }

            var stabilized = StabilizeLocation(rawLocation);
            var displayLocation = ApplyStationaryLock(stabilized, rawLocation);
            var rawToDisplayMeters = Location.CalculateDistance(rawLocation, displayLocation, DistanceUnits.Kilometers) * 1000d;

            Trace(
                $"GPS source={source} raw=({rawLocation.Latitude:F6},{rawLocation.Longitude:F6}) " +
                $"display=({displayLocation.Latitude:F6},{displayLocation.Longitude:F6}) " +
                $"rawAcc={rawAccuracyDisplay} rawAge={(rawAgeSeconds < 0 ? "n/a" : $"{rawAgeSeconds:0.0}s")} " +
                $"freshForAuto={hasFreshFixForAutoSelect} fetchMs={gpsFetchMs} " +
                $"rawToDisplay={rawToDisplayMeters:0.0}m");

            // [FIX-1] Truyền rawLocation vào SnapLocationToTourPath để quyết định có snap hay không
            var routeSnappedDisplayLocation = SnapLocationToTourPath(displayLocation, rawLocation);
            if (!ReferenceEquals(routeSnappedDisplayLocation, displayLocation))
            {
                var snapShiftMeters = Location.CalculateDistance(displayLocation, routeSnappedDisplayLocation, DistanceUnits.Kilometers) * 1000d;
                Trace($"GPS_ROUTE_SNAP shift={snapShiftMeters:0.0}m from=({displayLocation.Latitude:F6},{displayLocation.Longitude:F6}) to=({routeSnappedDisplayLocation.Latitude:F6},{routeSnappedDisplayLocation.Longitude:F6})");
            }

            ApplyLocationAndRefresh(
                routeSnappedDisplayLocation,
                rawLocation,
                forceMapRefresh,
                allowAutoSelect: hasFreshFixForAutoSelect,
                autoSelectLocation: routeSnappedDisplayLocation);
        }
        catch (Exception ex)
        {
            Trace($"GPS cycle error: {ex.Message}");
        }
        finally
        {
            Trace($"GPS cycle end elapsed={cycleSw.ElapsedMilliseconds}ms");
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

    private bool TryAutoSelectNearestStop(Location userLocation, Location? rawLocation)
    {
        var nowUtc = DateTime.UtcNow;

        if (PoiStops.Count == 0)
        {
            _currentAutoInsideZoneId = null;
            _lastAutoSelectTriggeredZoneId = null;
            Trace("AUTO_SELECT skip reason=no-stops");
            return false;
        }

        if (_currentAutoInsideZoneId is int currentZoneId)
        {
            var currentStop = PoiStops.FirstOrDefault(x => x.ZoneId == currentZoneId);
            if (currentStop != null)
            {
                var currentDistanceMeters = GetPoiDistanceMeters(currentStop, userLocation, rawLocation);

                var userAccuracyCompensation = GetAccuracyCompensationMeters(userLocation);
                var currentTriggerRadius = Math.Max(
                    currentStop.Radius > 0 ? currentStop.Radius : GeofenceTriggerMeters,
                    MinPracticalGeofenceMeters);
                var exitRadius = (currentTriggerRadius * GeofenceExitHysteresisFactor) + userAccuracyCompensation;

                if (currentDistanceMeters <= exitRadius)
                {
                    _currentZoneOutsideSamples = 0;
                    ResetAutoSelectCandidate();
                    Trace(
                        $"AUTO_SELECT hold zone={currentZoneId} reason=still-inside-exit-radius " +
                        $"dist={currentDistanceMeters:0.0}m exitRadius={exitRadius:0.0}m");
                    return false;
                }

                _currentZoneOutsideSamples += 1;
                if (_currentZoneOutsideSamples < CurrentZoneExitConfirmSamples)
                {
                    Trace($"AUTO_SELECT keep zone={currentZoneId} outsideSamples={_currentZoneOutsideSamples}/{CurrentZoneExitConfirmSamples}");
                    ResetAutoSelectCandidate();
                    return false;
                }
            }

            _currentAutoInsideZoneId = null;
            _lastAutoSelectTriggeredZoneId = null;
            _currentZoneOutsideSamples = 0;
            Trace($"AUTO_SELECT release previousZone={currentZoneId} reason=confirmed-outside");
        }

        PoiStopItem? nearestStop = null;
        double nearestDistanceMeters = double.MaxValue;

        foreach (var stop in PoiStops)
        {
            var distanceMeters = GetPoiDistanceMeters(stop, userLocation, rawLocation);

            if (distanceMeters < nearestDistanceMeters)
            {
                nearestDistanceMeters = distanceMeters;
                nearestStop = stop;
            }
        }

        if (nearestStop == null)
        {
            ResetAutoSelectCandidate();
            Trace("AUTO_SELECT skip reason=no-nearest-stop");
            return false;
        }

        var entryAccuracyCompensationMeters = GetEntryAccuracyCompensationMeters(userLocation, rawLocation);
        var adjustedDistanceMeters = Math.Max(0d, nearestDistanceMeters - entryAccuracyCompensationMeters);

        double triggerRadius = Math.Max(
            nearestStop.Radius > 0 ? nearestStop.Radius : GeofenceTriggerMeters,
            MinPracticalGeofenceMeters);

        if (adjustedDistanceMeters > triggerRadius)
        {
            ResetAutoSelectCandidate();
            Trace(
                $"AUTO_SELECT skip reason=outside-trigger nearestZone={nearestStop.ZoneId} " +
                $"dist={adjustedDistanceMeters:0.0}m trigger={triggerRadius:0.0}m");
            return false;
        }

        var currentSelected = PoiStops.FirstOrDefault(x => x.IsSelected);
        if (currentSelected?.ZoneId == nearestStop.ZoneId)
        {
            ResetAutoSelectCandidate();
            Trace(
                $"AUTO_SELECT skip reason=already-selected zone={nearestStop.ZoneId} " +
                $"dist={adjustedDistanceMeters:0.0}m trigger={triggerRadius:0.0}m");
            return false;
        }

        var isSwitchingBetweenStops = currentSelected != null && currentSelected.ZoneId != nearestStop.ZoneId;
        var requiredDebounce = isSwitchingBetweenStops ? AutoSelectSwitchDebounce : AutoSelectDebounce;
        var requiredInsideSamples = CandidateEnterConfirmSamples;

        var displayAccuracy = GetLocationAccuracyMeters(userLocation);
        var rawAcc = rawLocation == null ? double.MaxValue : GetLocationAccuracyMeters(rawLocation);

        // [FIX-2] Tính bestAccuracy: bỏ qua giá trị unknown (double.MaxValue)
        double bestAccuracy;
        if (displayAccuracy == double.MaxValue && rawAcc == double.MaxValue)
            bestAccuracy = double.MaxValue;
        else if (displayAccuracy == double.MaxValue)
            bestAccuracy = rawAcc;
        else if (rawAcc == double.MaxValue)
            bestAccuracy = displayAccuracy;
        else
            bestAccuracy = Math.Min(displayAccuracy, rawAcc);

        if (adjustedDistanceMeters <= FastAutoSelectDistanceMeters
            && bestAccuracy != double.MaxValue
            && bestAccuracy <= FastAutoSelectMaxAccuracyMeters)
        {
            requiredDebounce = requiredDebounce <= FastAutoSelectDebounce
                ? requiredDebounce
                : FastAutoSelectDebounce;
            requiredInsideSamples = 1;
        }

        if (isSwitchingBetweenStops && adjustedDistanceMeters > AutoSwitchRequireDistanceMeters)
        {
            ResetAutoSelectCandidate();
            Trace(
                $"AUTO_SELECT skip reason=switch-too-far from={currentSelected?.ZoneId} to={nearestStop.ZoneId} " +
                $"dist={adjustedDistanceMeters:0.0}m switchLimit={AutoSwitchRequireDistanceMeters:0.0}m");
            return false;
        }

        if (_autoSelectCandidateZoneId != nearestStop.ZoneId)
        {
            _autoSelectCandidateZoneId = nearestStop.ZoneId;
            _autoSelectCandidateSinceUtc = nowUtc;
            _candidateInsideSamples = 1;
            Trace(
                $"AUTO_SELECT candidate zone={nearestStop.ZoneId} dist={adjustedDistanceMeters:0.0}m " +
                $"radius={triggerRadius:0.0}m debounce={requiredDebounce.TotalSeconds:0.0}s");
            return false;
        }

        _candidateInsideSamples += 1;
        if (_candidateInsideSamples < requiredInsideSamples)
        {
            Trace(
                $"AUTO_SELECT wait reason=inside-samples zone={nearestStop.ZoneId} " +
                $"samples={_candidateInsideSamples}/{requiredInsideSamples}");
            return false;
        }

        if (nowUtc - _autoSelectCandidateSinceUtc < requiredDebounce)
        {
            Trace(
                $"AUTO_SELECT wait reason=debounce zone={nearestStop.ZoneId} " +
                $"elapsed={(nowUtc - _autoSelectCandidateSinceUtc).TotalMilliseconds:0}ms " +
                $"required={requiredDebounce.TotalMilliseconds:0}ms");
            return false;
        }

        if (_lastAutoSelectTriggeredAtUtc != DateTime.MinValue
            && nowUtc - _lastAutoSelectTriggeredAtUtc < AutoSelectCooldown
            && _lastAutoSelectTriggeredZoneId != nearestStop.ZoneId)
        {
            Trace(
                $"AUTO_SELECT skip reason=cooldown from={_lastAutoSelectTriggeredZoneId?.ToString() ?? "none"} " +
                $"to={nearestStop.ZoneId} remaining={(AutoSelectCooldown - (nowUtc - _lastAutoSelectTriggeredAtUtc)).TotalMilliseconds:0}ms");
            return false;
        }

        SelectStop(nearestStop, true, fromAutoSelect: true);
        _currentAutoInsideZoneId = nearestStop.ZoneId;
        _lastAutoSelectTriggeredAtUtc = nowUtc;
        _lastAutoSelectTriggeredZoneId = nearestStop.ZoneId;
        _currentZoneOutsideSamples = 0;
        ResetAutoSelectCandidate();
        Trace(
            $"AUTO_SELECT TRIGGERED zone={nearestStop.ZoneId} dist={adjustedDistanceMeters:0.0}m " +
            $"radius={triggerRadius:0.0}m acc={(bestAccuracy == double.MaxValue ? "unknown" : $"{bestAccuracy:0.0}m")}");

        if (_trackedEnterZones.Add(nearestStop.ZoneId))
        {
            _ = _apiService.TrackEnterZoneAsync(nearestStop.ZoneId, userLocation.Latitude, userLocation.Longitude);
        }

        return true;
    }

    private bool ShouldRefreshMapByMovement(Location userLocation, out double movedMeters)
    {
        if (_lastMapRefreshLocation == null)
        {
            movedMeters = -1;
            return true;
        }

        movedMeters = Location.CalculateDistance(_lastMapRefreshLocation, userLocation, DistanceUnits.Kilometers) * 1000d;
        return movedMeters >= MapRefreshMoveThresholdMeters;
    }

    private void ResetStationaryLock()
    {
        _isStationaryLocked = false;
        _stationaryAnchorLocation = null;
        _stationaryCandidateSinceUtc = DateTime.MinValue;
    }

    private Location ApplyStationaryLock(Location stabilizedLocation, Location rawLocation)
    {
        var nowUtc = DateTime.UtcNow;
        if (_cachedUserLocation == null)
        {
            return stabilizedLocation;
        }

        var movedFromPreviousMeters = Location.CalculateDistance(
            _cachedUserLocation,
            stabilizedLocation,
            DistanceUnits.Kilometers) * 1000d;

        if (_isStationaryLocked)
        {
            var anchor = _stationaryAnchorLocation ?? _cachedUserLocation;
            var movedFromAnchorMeters = Location.CalculateDistance(
                anchor,
                stabilizedLocation,
                DistanceUnits.Kilometers) * 1000d;
            var rawMovedFromAnchorMeters = Location.CalculateDistance(
                anchor,
                rawLocation,
                DistanceUnits.Kilometers) * 1000d;

            var speed = rawLocation.Speed.GetValueOrDefault();
            if (movedFromAnchorMeters >= StationaryReleaseMoveMeters
                || rawMovedFromAnchorMeters >= StationaryReleaseRawMoveMeters
                || (speed > 0 && speed >= StationaryReleaseSpeedMetersPerSecond))
            {
                Trace($"STATIONARY_LOCK released movedFromAnchor={movedFromAnchorMeters:0.0}m rawMoved={rawMovedFromAnchorMeters:0.0}m speed={speed:0.00}m/s");
                ResetStationaryLock();
                _stationaryLockSuppressedUntilUtc = nowUtc + StationaryRelockCooldown;
                return stabilizedLocation;
            }

            return anchor;
        }

        if (nowUtc < _stationaryLockSuppressedUntilUtc)
        {
            return stabilizedLocation;
        }

        var speedForEnter = rawLocation.Speed.GetValueOrDefault();
        var accuracyForEnter = GetLocationAccuracyMeters(rawLocation);
        var accuracyForEnterOk = accuracyForEnter != double.MaxValue && accuracyForEnter <= StationaryEnterMaxAccuracyMeters;

        if (movedFromPreviousMeters <= StationaryEnterMoveMeters)
        {
            if ((speedForEnter <= 0 || speedForEnter <= StationaryEnterMaxSpeedMetersPerSecond)
                && (accuracyForEnter == double.MaxValue || accuracyForEnterOk)) // unknown accuracy vẫn cho phép enter stationary
            {
                if (_stationaryCandidateSinceUtc == DateTime.MinValue)
                {
                    _stationaryCandidateSinceUtc = nowUtc;
                    Trace($"STATIONARY_CANDIDATE started speed={speedForEnter:0.00}m/s acc={(accuracyForEnter == double.MaxValue ? "unknown" : $"{accuracyForEnter:0.0}m")}");
                }
                else if (nowUtc - _stationaryCandidateSinceUtc >= StationaryLockDebounce)
                {
                    _isStationaryLocked = true;
                    _stationaryAnchorLocation = _cachedUserLocation;
                    Trace($"STATIONARY_LOCK entered anchor=({_stationaryAnchorLocation.Latitude:F6},{_stationaryAnchorLocation.Longitude:F6})");
                    return _stationaryAnchorLocation;
                }
            }
            else
            {
                _stationaryCandidateSinceUtc = DateTime.MinValue;
            }
        }
        else
        {
            _stationaryCandidateSinceUtc = DateTime.MinValue;
        }

        return stabilizedLocation;
    }

    private static double GetPoiDistanceMeters(PoiStopItem stop, Location displayLocation, Location? rawLocation)
    {
        var stopLocation = new Location(stop.Latitude, stop.Longitude);
        var displayDistance = Location.CalculateDistance(displayLocation, stopLocation, DistanceUnits.Kilometers) * 1000d;

        if (!IsRawPoiAssistUsable(rawLocation))
        {
            return displayDistance;
        }

        var rawDistance = Location.CalculateDistance(rawLocation!, stopLocation, DistanceUnits.Kilometers) * 1000d;
        return Math.Min(displayDistance, rawDistance);
    }

    private static bool IsRawPoiAssistUsable(Location? rawLocation)
    {
        if (rawLocation == null) return false;

        var accuracy = GetLocationAccuracyMeters(rawLocation);
        // [FIX-2] Bỏ qua location không có accuracy metadata
        if (accuracy == double.MaxValue) return false;

        return accuracy <= RawPoiAssistMaxAccuracyMeters;
    }

    private void ResetAutoSelectCandidate()
    {
        _autoSelectCandidateZoneId = null;
        _autoSelectCandidateSinceUtc = DateTime.MinValue;
        _candidateInsideSamples = 0;
    }

    private static Location StabilizeLocation(Location rawLocation)
    {
        if (_cachedUserLocation == null)
        {
            return rawLocation;
        }

        var movedMeters = Location.CalculateDistance(_cachedUserLocation, rawLocation, DistanceUnits.Kilometers) * 1000d;
        var rawAccuracyMeters = GetLocationAccuracyMeters(rawLocation);

        // [FIX-2] Nếu accuracy unknown thì không dùng để lọc nhiễu — pass through
        if (rawAccuracyMeters != double.MaxValue && rawAccuracyMeters > MaxAcceptableGpsAccuracyMeters && movedMeters < PoorAccuracyRequireMoveMeters)
        {
            return _cachedUserLocation;
        }

        if (rawAccuracyMeters != double.MaxValue && rawAccuracyMeters <= GoodAccuracySkipSmoothingMeters && movedMeters >= GpsSmoothingThresholdMeters)
        {
            return rawLocation;
        }

        if (movedMeters < GpsJitterIgnoreMeters)
        {
            return _cachedUserLocation;
        }

        if (movedMeters < GpsSmoothingThresholdMeters)
        {
            var lat = _cachedUserLocation.Latitude + ((rawLocation.Latitude - _cachedUserLocation.Latitude) * GpsSmoothingAlpha);
            var lon = _cachedUserLocation.Longitude + ((rawLocation.Longitude - _cachedUserLocation.Longitude) * GpsSmoothingAlpha);
            return new Location(lat, lon);
        }

        return rawLocation;
    }

    private static bool IsLocationStale(Location location)
    {
        if (location.Timestamp == default)
        {
            return false;
        }

        return DateTimeOffset.UtcNow - location.Timestamp > MaxAcceptedLocationAge;
    }

    private static double GetAccuracyCompensationMeters(Location location)
    {
        var accuracy = GetLocationAccuracyMeters(location);
        // [FIX-2] Unknown accuracy → không bù
        if (accuracy == double.MaxValue) return 0;

        return Math.Min(accuracy, MaxGpsAccuracyCompensationMeters);
    }

    private static double GetEntryAccuracyCompensationMeters(Location displayLocation, Location? rawLocation)
    {
        var displayAcc = GetLocationAccuracyMeters(displayLocation);
        var rawAcc = rawLocation == null ? double.MaxValue : GetLocationAccuracyMeters(rawLocation);

        // [FIX-2] Chỉ tính bù khi có accuracy thực sự
        double bestAcc;
        if (displayAcc == double.MaxValue && rawAcc == double.MaxValue)
            return 0; // cả hai đều unknown → không bù
        else if (displayAcc == double.MaxValue)
            bestAcc = rawAcc;
        else if (rawAcc == double.MaxValue)
            bestAcc = displayAcc;
        else
            bestAcc = Math.Min(displayAcc, rawAcc);

        if (bestAcc > EntryAccuracyCompensationEligibleMeters)
        {
            return 0;
        }

        return Math.Min(bestAcc * 0.35d, MaxEntryAccuracyCompensationMeters);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // [FIX-2] GetLocationAccuracyMeters: trả về double.MaxValue khi không có
    // accuracy metadata thay vì 0, để phân biệt "unknown" khỏi "rất tốt".
    //
    // Tất cả caller cần kiểm tra:  accuracy == double.MaxValue  → unknown
    // Thay vì kiểm tra cũ:         accuracy <= 0               → unknown
    // ─────────────────────────────────────────────────────────────────────────
    private static double GetLocationAccuracyMeters(Location location)
    {
        var accuracy = location.Accuracy;
        if (!accuracy.HasValue || accuracy.Value <= 0)
            return double.MaxValue; // unknown — không có metadata

        return accuracy.Value;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // [FIX-1] SnapLocationToTourPath: Thêm tham số rawLocation để kiểm tra
    // accuracy trước khi snap. Khi GPS đã chính xác hơn RouteSnapMaxDistanceMeters
    // thì snap sẽ inject thêm sai số — bỏ qua hoàn toàn.
    // ─────────────────────────────────────────────────────────────────────────
    private Location SnapLocationToTourPath(Location location, Location? rawLocation = null)
    {
        if (PoiStops.Count < 2)
        {
            return location;
        }

        // [FIX-1] Bỏ qua snap khi GPS đã chính xác hơn ngưỡng snap
        // Lý do: snap có thể gây sai lệch tới RouteSnapMaxDistanceMeters,
        // nếu rawAccuracy < ngưỡng này thì snap làm tệ hơn thay vì tốt hơn.
        var checkLocation = rawLocation ?? location;
        var bestAccuracy = GetLocationAccuracyMeters(checkLocation);
        if (bestAccuracy != double.MaxValue && bestAccuracy <= RouteSnapSkipWhenAccuracyBetterThanMeters)
        {
            Trace($"GPS_ROUTE_SNAP skip reason=accuracy-good acc={bestAccuracy:0.0}m threshold={RouteSnapSkipWhenAccuracyBetterThanMeters:0.0}m");
            return location;
        }

        var orderedStops = PoiStops
            .OrderBy(s => s.OrderIndex)
            .ToList();

        var originLatRad = location.Latitude * Math.PI / 180d;
        var metersPerDegLat = 111_320d;
        var metersPerDegLon = 111_320d * Math.Cos(originLatRad);
        if (metersPerDegLon <= 0.000001d)
        {
            return location;
        }

        var px = location.Longitude * metersPerDegLon;
        var py = location.Latitude * metersPerDegLat;

        var bestDistSquared = double.MaxValue;
        double bestProjX = px;
        double bestProjY = py;
        var foundProjection = false;

        for (var i = 0; i < orderedStops.Count - 1; i++)
        {
            var a = orderedStops[i];
            var b = orderedStops[i + 1];

            var ax = a.Longitude * metersPerDegLon;
            var ay = a.Latitude * metersPerDegLat;
            var bx = b.Longitude * metersPerDegLon;
            var by = b.Latitude * metersPerDegLat;

            var vx = bx - ax;
            var vy = by - ay;
            var lenSquared = (vx * vx) + (vy * vy);
            if (lenSquared <= 0.0001d)
            {
                continue;
            }

            var t = ((px - ax) * vx + (py - ay) * vy) / lenSquared;
            if (t < 0d) t = 0d;
            if (t > 1d) t = 1d;

            var projX = ax + (t * vx);
            var projY = ay + (t * vy);
            var dx = px - projX;
            var dy = py - projY;
            var distSquared = (dx * dx) + (dy * dy);

            if (distSquared < bestDistSquared)
            {
                bestDistSquared = distSquared;
                bestProjX = projX;
                bestProjY = projY;
                foundProjection = true;
            }
        }

        if (!foundProjection)
        {
            return location;
        }

        var bestDistMeters = Math.Sqrt(bestDistSquared);
        if (bestDistMeters > RouteSnapMaxDistanceMeters)
        {
            Trace($"GPS_ROUTE_SNAP skip reason=too-far-from-path dist={bestDistMeters:0.0}m maxSnap={RouteSnapMaxDistanceMeters:0.0}m");
            return location;
        }

        var snappedLat = bestProjY / metersPerDegLat;
        var snappedLon = bestProjX / metersPerDegLon;
        return new Location(snappedLat, snappedLon);
    }

    private void ApplyLocationAndRefresh(Location location, Location? rawLocationForPoi, bool forceMapRefresh, bool allowAutoSelect, Location? autoSelectLocation = null)
    {
        var nowUtc = DateTime.UtcNow;
        var applySeq = Interlocked.Increment(ref _locationApplySeq);
        var sinceLastApplyMs = _lastLocationAppliedAtUtc == DateTime.MinValue
            ? -1d
            : (nowUtc - _lastLocationAppliedAtUtc).TotalMilliseconds;
        _lastLocationAppliedAtUtc = nowUtc;

        UserLocation = location;
        _cachedUserLocation = location;

        var activePingInterval = IsPoiSimulationEnabled ? SimulationLocationPingInterval : LocationPingInterval;
        if (DateTime.UtcNow - _lastLocationPingAtUtc >= activePingInterval)
        {
            _lastLocationPingAtUtc = DateTime.UtcNow;
            _ = _apiService.TrackLocationPingAsync(location.Latitude, location.Longitude);
        }

        foreach (var stop in PoiStops)
        {
            var distanceKm = Location.CalculateDistance(
                location,
                new Location(stop.Latitude, stop.Longitude),
                DistanceUnits.Kilometers);

            stop.DistanceText = $"{distanceKm:0.0} km";
        }

        UpdateRemoteExploreHint(location);

        var autoSelectReferenceLocation = autoSelectLocation ?? location;
        var selectionChanged = allowAutoSelect && TryAutoSelectNearestStop(autoSelectReferenceLocation, rawLocationForPoi);
        var movementTriggered = ShouldRefreshMapByMovement(location, out var movedSinceMapRefreshMeters);
        var shouldRefreshMap = forceMapRefresh || selectionChanged || movementTriggered;
        var rawPoiAcc = rawLocationForPoi == null ? double.MaxValue : GetLocationAccuracyMeters(rawLocationForPoi);
        var rawPoiAccDisplay = rawPoiAcc == double.MaxValue ? "unknown" : $"{rawPoiAcc:0.0}m";

        Trace(
            $"APPLY#{applySeq} dt={(sinceLastApplyMs < 0 ? "n/a" : $"{sinceLastApplyMs:0}ms")} " +
            $"loc=({location.Latitude:F6},{location.Longitude:F6}) rawPoiAcc={rawPoiAccDisplay} " +
            $"refresh?={shouldRefreshMap} force={forceMapRefresh} select={selectionChanged} " +
            $"moveTrigger={movementTriggered} moveSinceMap={(movedSinceMapRefreshMeters < 0 ? "n/a" : $"{movedSinceMapRefreshMeters:0.0}m")}");

        if (shouldRefreshMap)
        {
            _lastMapRefreshLocation = location;
            _lastMapRefreshAtUtc = nowUtc;
            var refreshSeq = Interlocked.Increment(ref _mapRefreshSeq);
            Trace($"MAP_REFRESH#{refreshSeq} emit MapDataChanged");
            MapDataChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void UpdateRemoteExploreHint(Location? location)
    {
        if (location == null)
        {
            CancelRemoteHintAutoHide();
            _remoteExploreHintShownInCurrentFarSession = false;
            IsRemoteExploreHintVisible = false;
            RemoteExploreHintText = string.Empty;
            return;
        }

        var buiVien = new Location(BuiVienLatitude, BuiVienLongitude);
        var distanceKm = Location.CalculateDistance(location, buiVien, DistanceUnits.Kilometers);
        var distanceMeters = distanceKm * 1000d;

        if (distanceMeters < RemoteExploreHintThresholdMeters)
        {
            CancelRemoteHintAutoHide();
            _remoteExploreHintShownInCurrentFarSession = false;
            IsRemoteExploreHintVisible = false;
            RemoteExploreHintText = string.Empty;
            return;
        }

        RemoteExploreHintText = _localizationManager.Format("tour_remote_explore_hint", distanceKm);

        if (_remoteExploreHintShownInCurrentFarSession)
        {
            return;
        }

        IsRemoteExploreHintVisible = true;
        _remoteExploreHintShownInCurrentFarSession = true;
        StartRemoteHintAutoHide();
    }

    private void StartRemoteHintAutoHide()
    {
        CancelRemoteHintAutoHide();

        var cts = new CancellationTokenSource();
        _remoteHintAutoHideCts = cts;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(RemoteExploreHintDisplayDuration, cts.Token);
                if (cts.IsCancellationRequested) return;
                IsRemoteExploreHintVisible = false;
            }
            catch (OperationCanceledException)
            {
            }
        });
    }

    private void CancelRemoteHintAutoHide()
    {
        _remoteHintAutoHideCts?.Cancel();
        _remoteHintAutoHideCts?.Dispose();
        _remoteHintAutoHideCts = null;
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
    public int Radius { get; init; }
    public bool IsMain { get; init; }
    public string Address { get; init; } = "--";
    public string Hours { get; init; } = "--";

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