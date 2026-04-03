using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Devices.Sensors;
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
    private DateTime _autoSelectSuppressedUntilUtc = DateTime.MinValue;
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

    // Cache vị trí tĩnh giữa các lần điều hướng để tránh GPS cold-start
    private static Location? _cachedUserLocation;
    private string _tourName = "Tour details";

    private const double GeofenceTriggerMeters = 45;
    private const double GeofenceExitHysteresisFactor = 1.25;
    private const double MinPracticalGeofenceMeters = 28;
    private const double MaxGpsAccuracyCompensationMeters = 35;
    private const double MaxEntryAccuracyCompensationMeters = 4;
    private const double EntryAccuracyCompensationEligibleMeters = 12;
    private const double AutoSwitchRequireDistanceMeters = 20;
    private const double FastAutoSelectDistanceMeters = 10;
    private const double FastAutoSelectMaxAccuracyMeters = 8;
    private const int CurrentZoneExitConfirmSamples = 2;
    private const int CandidateEnterConfirmSamples = 2;
    private const double MapRefreshMoveThresholdMeters = 2.5;
    private const double GpsJitterIgnoreMeters = 3.5;
    private const double GpsSmoothingThresholdMeters = 12;
    private const double GpsSmoothingAlpha = 0.55;
    private const double GoodAccuracySkipSmoothingMeters = 6;
    private const double MaxAcceptableGpsAccuracyMeters = 45;
    private const double PoorAccuracyRequireMoveMeters = 18;
    private const double StationaryEnterMoveMeters = 2;
    private const double StationaryReleaseMoveMeters = 9;
    private const double StationaryReleaseRawMoveMeters = 5;
    private const double StationaryReleaseSpeedMetersPerSecond = 1.2;
    private const double StationaryEnterMaxSpeedMetersPerSecond = 0.35;
    private const double StationaryEnterMaxAccuracyMeters = 25;
    private const double RawPoiAssistMaxAccuracyMeters = 30;
    private const double BuiVienLatitude = 10.764017;
    private const double BuiVienLongitude = 106.692527;
    private const double RemoteExploreHintThresholdMeters = 1000;
    private static readonly TimeSpan ForegroundTrackingInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan MaxAcceptedLocationAge = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan StationaryLockDebounce = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan StationaryRelockCooldown = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan LocationPingInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan AutoSelectDebounce = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan AutoSelectSwitchDebounce = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan FastAutoSelectDebounce = TimeSpan.FromSeconds(1.2);
    private static readonly TimeSpan AutoSelectCooldown = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan AutoSelectSuppressAfterManualSelection = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan StreamFreshWindow = TimeSpan.FromSeconds(2.6);
    private static readonly TimeSpan PollFallbackInterval = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan SimulationStepInterval = TimeSpan.FromMilliseconds(700);
    private static readonly TimeSpan SimulationPauseAtPoi = TimeSpan.FromSeconds(5);

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

    private async Task NavigateToZoneDetail(PoiStopItem stop)
    {
        if (stop == null) return;

        // Select the stop locally first for UI feedback (SILENT = No Audio)
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
            _trackedEnterZones.Clear();
            ResetAutoSelectCandidate();
            _lastAutoSelectTriggeredAtUtc = DateTime.MinValue;
            _lastAutoSelectTriggeredZoneId = null;
            _currentAutoInsideZoneId = null;
            _simulatedLocation = null;
            ResetStationaryLock();
            RemoteExploreHintText = string.Empty;
            IsRemoteExploreHintVisible = false;
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
        if (TourId < 0) return;
        if (IsLoading) return;

        await _loadLock.WaitAsync();
        var loadingStartedAt = DateTime.UtcNow;
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
                // 1. Lấy từ API (Server)
                var remoteFavorites = await _apiService.GetFavoritesAsync(guestId);
                if (remoteFavorites != null)
                {
                    foreach (var f in remoteFavorites) favoriteZoneIds.Add(f.ZoneId);
                }

                // 2. Lấy từ DB cục bộ (Bao gồm các mục chưa sync hoặc local-only như Cổng Chào)
                var localFavorites = await _dbService.GetLocalFavoritesAsync(guestId);
                if (localFavorites != null)
                {
                    foreach (var f in localFavorites)
                    {
                        if (f.IsDeleted == 0)
                            favoriteZoneIds.Add(f.ZoneId);
                        else
                            favoriteZoneIds.Remove(f.ZoneId); // Đã bị xóa local
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

            /* 
            // Removed default selection as per user request
            if (PoiStops.Count > 0)
            {
                PoiStops[0].IsSelected = true;
                Trace($"Initial selected ZoneId={PoiStops[0].ZoneId}");
            }
            */

            MapDataChanged?.Invoke(this, EventArgs.Empty);
            Trace("MapDataChanged invoked after loading stops");

            _locationCts?.Cancel();
            _locationCts?.Dispose();
            _locationCts = new CancellationTokenSource();
            var token = _locationCts.Token;

            if (_isForegroundTracking)
            {
                // Keep continuous tracking alive after data reload.
                _ = Task.Run(() => ForegroundTrackingLoopAsync(token));
            }
            else
            {
                // One-shot location refresh when foreground tracking is off.
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
        // Chạy trên background thread để tránh block main thread khi xin quyền GPS
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

        if (_trackedEnterZones.Add(stop.ZoneId))
        {
            _ = _apiService.TrackEnterZoneAsync(stop.ZoneId, stop.Latitude, stop.Longitude);
        }
    }

    private async Task ToggleFavorite(PoiStopItem stop)
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

            // Offline-first: enqueue local op when no network
            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
            {
                if (stop.IsFavorite)
                {
                    // remove locally
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
                    // add locally
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

                // trigger background sync to reconcile any pending ops
                _ = Task.Run(() => _apiService.SyncFavoritesIfOnlineAsync());
            }

            if (!success)
            {
                Trace($"ToggleFavorite failed for ZoneId={stop.ZoneId} (perhaps it's a marker only)");
                // Optimistic: Even if API fails (e.g. invalid server ID), allow local favorite for UI feedback
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

    private void SelectStop(PoiStopItem stop, bool triggerEvent = true, bool fromAutoSelect = false)
    {
        if (stop == null) return;

        foreach (var poi in PoiStops)
        {
            poi.IsSelected = poi.ZoneId == stop.ZoneId;
        }

        // Đẩy zone được chọn lên đầu danh sách
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

        if (!fromAutoSelect)
        {
            // Keep manual selection sticky to avoid immediate auto-switching when nearby POI overlap.
            _autoSelectSuppressedUntilUtc = DateTime.UtcNow + AutoSelectSuppressAfterManualSelection;
            _currentAutoInsideZoneId = stop.ZoneId;
            _currentZoneOutsideSamples = 0;
            _lastAutoSelectTriggeredAtUtc = DateTime.UtcNow;
            _lastAutoSelectTriggeredZoneId = stop.ZoneId;
            ResetAutoSelectCandidate();
            Trace($"MANUAL_SELECT zone={stop.ZoneId} suppressAuto={AutoSelectSuppressAfterManualSelection.TotalSeconds:0}s");
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

            // Hybrid strategy:
            // 1) Prefer latest stream location (low latency, stable cadence)
            // 2) Fallback to polling only when stream is stale/unavailable
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
                freshLocation = streamLocation;
            }
            else if (_lastPollingAttemptAtUtc == DateTime.MinValue || nowUtc - _lastPollingAttemptAtUtc >= PollFallbackInterval)
            {
                _lastPollingAttemptAtUtc = nowUtc;
                source = "poll";
                var highAccuracyFix = await Geolocation.Default.GetLocationAsync(
                    new GeolocationRequest(GeolocationAccuracy.High, TimeSpan.FromSeconds(1.5)),
                    cancellationToken);
                freshLocation = highAccuracyFix
                    ?? await Geolocation.Default.GetLocationAsync(
                        new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(0.9)),
                        cancellationToken);
            }
            else
            {
                source = "stream-stale-no-poll";
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
            var rawAccuracy = GetLocationAccuracyMeters(rawLocation);
            var rawAgeSeconds = rawLocation.Timestamp == default
                ? -1
                : (DateTimeOffset.UtcNow - rawLocation.Timestamp).TotalSeconds;
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
                $"rawAcc={rawAccuracy:0.0}m rawAge={(rawAgeSeconds < 0 ? "n/a" : $"{rawAgeSeconds:0.0}s")} " +
                $"freshForAuto={hasFreshFixForAutoSelect} fetchMs={gpsFetchMs} " +
                $"rawToDisplay={rawToDisplayMeters:0.0}m");
            ApplyLocationAndRefresh(displayLocation, rawLocation, forceMapRefresh, allowAutoSelect: hasFreshFixForAutoSelect);
        }
        catch (Exception ex)
        {
            // Keep default "--" if location unavailable.
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
        if (DateTime.UtcNow < _autoSelectSuppressedUntilUtc)
        {
            return false;
        }

        if (PoiStops.Count == 0)
        {
            _currentAutoInsideZoneId = null;
            _lastAutoSelectTriggeredZoneId = null;
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
            return false;
        }

        var entryAccuracyCompensationMeters = GetEntryAccuracyCompensationMeters(userLocation, rawLocation);
        var adjustedDistanceMeters = Math.Max(0d, nearestDistanceMeters - entryAccuracyCompensationMeters);

        // Use the stop's radius if available, otherwise fallback to GeofenceTriggerMeters (80m).
        // Keep a practical minimum radius to tolerate real-world GPS drift.
        double triggerRadius = Math.Max(
            nearestStop.Radius > 0 ? nearestStop.Radius : GeofenceTriggerMeters,
            MinPracticalGeofenceMeters);

        if (adjustedDistanceMeters > triggerRadius)
        {
            ResetAutoSelectCandidate();
            return false;
        }

        var currentSelected = PoiStops.FirstOrDefault(x => x.IsSelected);
        if (currentSelected?.ZoneId == nearestStop.ZoneId)
        {
            ResetAutoSelectCandidate();
            return false;
        }

        var nowUtc = DateTime.UtcNow;
        var isSwitchingBetweenStops = currentSelected != null && currentSelected.ZoneId != nearestStop.ZoneId;
        var requiredDebounce = isSwitchingBetweenStops ? AutoSelectSwitchDebounce : AutoSelectDebounce;
        var requiredInsideSamples = CandidateEnterConfirmSamples;

        // Fast path: allow quicker capture when user is very close to POI and GPS quality is good.
        var displayAccuracy = GetLocationAccuracyMeters(userLocation);
        var rawAccuracy = rawLocation == null ? 0 : GetLocationAccuracyMeters(rawLocation);
        var bestAccuracy = displayAccuracy > 0 && rawAccuracy > 0
            ? Math.Min(displayAccuracy, rawAccuracy)
            : Math.Max(displayAccuracy, rawAccuracy);
        if (adjustedDistanceMeters <= FastAutoSelectDistanceMeters
            && bestAccuracy > 0
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
            return false;
        }

        if (nowUtc - _autoSelectCandidateSinceUtc < requiredDebounce)
        {
            return false;
        }

        if (_lastAutoSelectTriggeredAtUtc != DateTime.MinValue
            && nowUtc - _lastAutoSelectTriggeredAtUtc < AutoSelectCooldown
            && _lastAutoSelectTriggeredZoneId != nearestStop.ZoneId)
        {
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
            $"radius={triggerRadius:0.0}m");

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
        if (movedFromPreviousMeters <= StationaryEnterMoveMeters)
        {
            if ((speedForEnter <= 0 || speedForEnter <= StationaryEnterMaxSpeedMetersPerSecond)
                && (accuracyForEnter <= 0 || accuracyForEnter <= StationaryEnterMaxAccuracyMeters))
            {
                if (_stationaryCandidateSinceUtc == DateTime.MinValue)
                {
                    _stationaryCandidateSinceUtc = nowUtc;
                }
                else if (nowUtc - _stationaryCandidateSinceUtc >= StationaryLockDebounce)
                {
                    _isStationaryLocked = true;
                    _stationaryAnchorLocation = _cachedUserLocation;
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
        if (rawLocation == null)
        {
            return false;
        }

        var accuracy = GetLocationAccuracyMeters(rawLocation);
        if (accuracy <= 0)
        {
            return false;
        }

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
        if (rawAccuracyMeters > MaxAcceptableGpsAccuracyMeters && movedMeters < PoorAccuracyRequireMoveMeters)
        {
            // Bỏ fix quá nhiễu khi người dùng gần như chưa di chuyển.
            return _cachedUserLocation;
        }

        if (rawAccuracyMeters > 0 && rawAccuracyMeters <= GoodAccuracySkipSmoothingMeters && movedMeters >= GpsSmoothingThresholdMeters)
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
        if (accuracy <= 0)
        {
            return 0;
        }

        return Math.Min(accuracy, MaxGpsAccuracyCompensationMeters);
    }

    private static double GetEntryAccuracyCompensationMeters(Location displayLocation, Location? rawLocation)
    {
        var displayAcc = GetLocationAccuracyMeters(displayLocation);
        var rawAcc = rawLocation == null ? 0 : GetLocationAccuracyMeters(rawLocation);

        var bestAcc = displayAcc > 0 && rawAcc > 0
            ? Math.Min(displayAcc, rawAcc)
            : Math.Max(displayAcc, rawAcc);

        if (bestAcc <= 0 || bestAcc > EntryAccuracyCompensationEligibleMeters)
        {
            return 0;
        }

        // Entry compensation chỉ nhỏ để tránh false-positive lúc còn ở ngoài zone.
        return Math.Min(bestAcc * 0.35d, MaxEntryAccuracyCompensationMeters);
    }

    private static double GetLocationAccuracyMeters(Location location)
    {
        var accuracy = location.Accuracy;
        if (!accuracy.HasValue || accuracy.Value <= 0)
        {
            return 0;
        }

        return accuracy.Value;
    }

    private void ApplyLocationAndRefresh(Location location, Location? rawLocationForPoi, bool forceMapRefresh, bool allowAutoSelect)
    {
        var nowUtc = DateTime.UtcNow;
        var applySeq = Interlocked.Increment(ref _locationApplySeq);
        var sinceLastApplyMs = _lastLocationAppliedAtUtc == DateTime.MinValue
            ? -1d
            : (nowUtc - _lastLocationAppliedAtUtc).TotalMilliseconds;
        _lastLocationAppliedAtUtc = nowUtc;

        UserLocation = location;
        _cachedUserLocation = location;

        if (DateTime.UtcNow - _lastLocationPingAtUtc >= LocationPingInterval)
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

        var selectionChanged = allowAutoSelect && TryAutoSelectNearestStop(location, rawLocationForPoi);
        var movementTriggered = ShouldRefreshMapByMovement(location, out var movedSinceMapRefreshMeters);
        var shouldRefreshMap = forceMapRefresh || selectionChanged || movementTriggered;
        var rawPoiAcc = rawLocationForPoi == null ? 0 : GetLocationAccuracyMeters(rawLocationForPoi);
        Trace(
            $"APPLY#{applySeq} dt={(sinceLastApplyMs < 0 ? "n/a" : $"{sinceLastApplyMs:0}ms")} " +
            $"loc=({location.Latitude:F6},{location.Longitude:F6}) rawPoiAcc={rawPoiAcc:0.0}m " +
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
            IsRemoteExploreHintVisible = false;
            RemoteExploreHintText = string.Empty;
            return;
        }

        var buiVien = new Location(BuiVienLatitude, BuiVienLongitude);
        var distanceKm = Location.CalculateDistance(location, buiVien, DistanceUnits.Kilometers);
        var distanceMeters = distanceKm * 1000d;

        if (distanceMeters < RemoteExploreHintThresholdMeters)
        {
            IsRemoteExploreHintVisible = false;
            RemoteExploreHintText = string.Empty;
            return;
        }

        RemoteExploreHintText = _localizationManager.Format("tour_remote_explore_hint", distanceKm);
        IsRemoteExploreHintVisible = true;
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
