using System.Diagnostics;
using Mapsui;
using Mapsui.Extensions;
using Mapsui.Layers;
using Mapsui.Projections;
using Mapsui.Providers;
using Mapsui.Styles;
using Mapsui.Tiling;
using Mapsui.UI.Maui;
using Mapsui.Nts.Extensions;
using NetTopologySuite.Geometries;
using TravelSystem.Mobile.ViewModels;
using CommunityToolkit.Maui.Views;
using TravelSystem.Mobile.Services;
using Itinero;
using Itinero.Osm.Vehicles;
using Itinero.LocalGeo;
using Mapsui.Nts;

namespace TravelSystem.Mobile.Views;

public partial class TourDetailPage : ContentPage
{
    private const double BuiVienLatitude = 10.764017;
    private const double BuiVienLongitude = 106.692527;
    private const double MinFocusMeters = 550;

    // Mapsui layers (Style = null để tắt điểm tròn trắng mặc định)
    private readonly MemoryLayer _poiLayer   = new() { Name = "PoiLayer", Style = null };
    private readonly MemoryLayer _userLayer  = new() { Name = "UserLayer", Style = null };
    private readonly MemoryLayer _routeLayer = new() 
    { 
        Name = "RouteLayer",
        Style = new Mapsui.Styles.VectorStyle 
        { 
            Line = new Mapsui.Styles.Pen(Mapsui.Styles.Color.FromArgb(200, 76, 175, 80), 4) // Green for walking
        } 
    };

    private readonly TourDetailViewModel _viewModel;
    private readonly IAudioGuideService _audioService;
    private readonly DatabaseService _dbService;
    private MapControl? _mapControl;
    private Mapsui.Navigator? _trackedNavigator;

    private Router? _router;
    private Itinero.Profiles.Profile? _routingProfile;
    private bool _isRoutingInitialized;

    private string? _defaultSvg;
    private string? _selectedSvg;
    private CancellationTokenSource? _renderCts;
    private CancellationTokenSource? _viewportRenderCts;

    private bool _hasFocusedOnce;
    private int _lastSelectedZoneId = -1;
    private string? _lastStaticMapKey;
    private (double Latitude, double Longitude)? _lastRenderedUserLocation;
    private bool _isMapInitialized;
    private Task? _mapInitTask;
    private MRect? _lastViewportExtent;
    private static readonly List<IFeature> EmptyFeatures = [];

    // ─── Bottom Sheet ────────────────────────────────────────────────────────
    // 3 snap points (tỉ lệ chiều cao màn hình: 0=top, 1=bottom), tăng dần
    private static readonly double[] SnapPoints = { 0.1, 0.75, 0.94 };

    private double _sheetSnapY     = 0.94;  // snap hiện tại (bắt đầu ở Collapsed)
    private double _panStartTransY = 0;     // TranslationY lúc bắt đầu kéo
    private double _lastPanTotalY  = 0;     // TotalY tích luỹ gesture hiện tại
    private bool   _isPanning      = false;


    public TourDetailPage(TourDetailViewModel viewModel, IAudioGuideService audioService, DatabaseService dbService)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _audioService = audioService;
        _dbService = dbService;
        BindingContext = _viewModel;
        Shell.SetTabBarIsVisible(this, false);

        // --- DEBUG: BẮT LỖI NGẦM TỪ BÊN TRONG MAPSUI VÀ BRUTILE ---
        Mapsui.Logging.Logger.LogDelegate += (level, message, exception) =>
        {
            Debug.WriteLine($"[MAPSUI_CRASH_LOG] Level: {level} | Msg: {message} | Ex: {exception}");
        };

        // Subscribe to See Details request from Audio Player Popup
        AudioPlayer.SeeDetailsRequested += (stop) => 
        {
            _viewModel.NavigateToZoneDetailCommand.Execute(stop); // Navigate directly
        };
    }

    // ─── Bottom Sheet Drag ───────────────────────────────────────────────────

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (width <= 0 || height <= 0 || BottomSheet == null) return;
        if (_isPanning) return; // không reset khi đang kéo

        // WidthProportional: width=1.0 luôn = 100% màn hình → không bị cắt
        AbsoluteLayout.SetLayoutFlags(BottomSheet, Microsoft.Maui.Layouts.AbsoluteLayoutFlags.WidthProportional);
        AbsoluteLayout.SetLayoutBounds(BottomSheet, new Rect(0, _sheetSnapY * height, 1.0, height));
        BottomSheet.TranslationY = 0;
    }

    private void OnBottomSheetPanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        if (BottomSheet == null) return;
        var pageH = Height;
        if (pageH <= 0) return;

        switch (e.StatusType)
        {
            case GestureStatus.Started:
                _isPanning      = true;
                _panStartTransY = BottomSheet.TranslationY;
                _lastPanTotalY  = 0;
                break;

            case GestureStatus.Running:
                if (!_isPanning) break;
                _lastPanTotalY = e.TotalY;

                var raw      = _panStartTransY + e.TotalY;
                var baseY    = _sheetSnapY * pageH;
                var minTrans = SnapPoints[0] * pageH - baseY;      // giới hạn Expanded
                var maxTrans = SnapPoints[^1] * pageH - baseY;     // giới hạn Collapsed
                BottomSheet.TranslationY = Math.Clamp(raw, minTrans, maxTrans);
                break;

            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                if (!_isPanning) break;
                _isPanning = false;
                SnapByDirection(pageH, _lastPanTotalY);
                break;
        }
    }

    // Snap theo HƯỚNG kéo thay vì vị trí gần nhất
    private void SnapByDirection(double pageH, double dragTotalY)
    {
        const double threshold = 40; // px tối thiểu để chuyển sang state khác

        var currentIdx = Array.IndexOf(SnapPoints, _sheetSnapY);
        double target;

        if (dragTotalY < -threshold && currentIdx > 0)
            target = SnapPoints[currentIdx - 1];          // kéo LÊN → mở rộng
        else if (dragTotalY > threshold && currentIdx < SnapPoints.Length - 1)
            target = SnapPoints[currentIdx + 1];          // kéo XUỐNG → thu nhỏ
        else
            target = _sheetSnapY;                         // kéo nhẹ → giữ nguyên

        var fromY = _sheetSnapY * pageH + BottomSheet.TranslationY;
        _sheetSnapY = target;
        var toY = target * pageH;

        var anim = new Animation(v =>
        {
            AbsoluteLayout.SetLayoutFlags(BottomSheet, Microsoft.Maui.Layouts.AbsoluteLayoutFlags.WidthProportional);
            AbsoluteLayout.SetLayoutBounds(BottomSheet, new Rect(0, v, 1.0, pageH));
            BottomSheet.TranslationY = 0;
        }, fromY, toY);

        anim.Commit(this, "SheetSnap", length: 260, easing: Easing.CubicOut);
    }

    private void SnapToHalf()
    {
        if (BottomSheet == null) return;
        var pageH = Height;
        if (pageH <= 0) return;

        double target = 0.75;
        var fromY = _sheetSnapY * pageH + BottomSheet.TranslationY;
        _sheetSnapY = target;
        var toY = target * pageH;

        var anim = new Animation(v =>
        {
            AbsoluteLayout.SetLayoutFlags(BottomSheet, Microsoft.Maui.Layouts.AbsoluteLayoutFlags.WidthProportional);
            AbsoluteLayout.SetLayoutBounds(BottomSheet, new Rect(0, v, 1.0, pageH));
            BottomSheet.TranslationY = 0;
        }, fromY, toY);

        anim.Commit(this, "SheetSnap", length: 260, easing: Easing.CubicOut);
    }

    // ─── Lifecycle ───────────────────────────────────────────────────────────

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Shell.SetTabBarIsVisible(this, false);
        _viewModel.StopSelected -= OnStopSelected;
        _viewModel.StopSelected += OnStopSelected;

        _viewModel.MapDataChanged -= OnMapDataChanged;
        _viewModel.MapDataChanged += OnMapDataChanged;
        _viewModel.StartForegroundTracking();

        if (_isMapInitialized)
        {
            // Quay lại trang → reset key để bắt buộc vẽ lại
            _lastStaticMapKey = null;
            RenderMap();
            FocusOnBuiVienAnchor();
        }
        else
        {
            _mapInitTask ??= InitializeMapAsync();
        }

        if (!_isRoutingInitialized)
        {
            _ = Task.Run(InitializeRoutingAsync);
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.StopForegroundTracking();
        _viewModel.MapDataChanged -= OnMapDataChanged;
        _viewModel.StopSelected -= OnStopSelected;

        if (_trackedNavigator != null)
        {
            _trackedNavigator.ViewportChanged -= OnViewportChanged;
            _trackedNavigator = null;
        }

        CancelAndDispose(ref _renderCts);
        CancelAndDispose(ref _viewportRenderCts);
    }

    // ─── Map Init ────────────────────────────────────────────────────────────

    private async Task InitializeMapAsync()
    {
        if (_isMapInitialized) return;

        Debug.WriteLine("[MAP_INIT] 1. Chờ MapHelper...");
        try 
        {
            await Services.MapHelper.EnsureOfflineMapExistsAsync().WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MAP_INIT] ❌ Lỗi MapHelper: {ex.Message}");
        }

        try 
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (_isMapInitialized) return;
                Debug.WriteLine("[MAP_INIT] 4. Đang khởi tạo bản đồ Offline...");

                try 
                {
                    var map = new Mapsui.Map();
                    map.CRS = "EPSG:3857";
                    
                    string mapPath = Services.MapHelper.GetOfflineMapPath();
                    bool offlineLoaded = false;

                    if (System.IO.File.Exists(mapPath))
                    {
                        try 
                        {
                            var connString = new SQLite.SQLiteConnectionString(mapPath, false);
                            var mbTilesSource = new BruTile.MbTiles.MbTilesTileSource(connString);
                            var offlineLayer = new Mapsui.Tiling.Layers.TileLayer(mbTilesSource) { Name = "OfflineMap" };
                            map.Layers.Add(offlineLayer);
                            offlineLoaded = true;
                            Debug.WriteLine($"[MAP_INIT] 5. Đã nạp thành công: {mapPath}");
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[MAP_INIT] ❌ Lỗi nạp MBTiles: {ex.Message}");
                        }
                    }

                    // Nếu không nạp được Offline (file thiếu hoặc lỗi) thì mới dùng OSM làm nền trắng
                    if (!offlineLoaded)
                    {
                        map.Layers.Add(Mapsui.Tiling.OpenStreetMap.CreateTileLayer());
                        Debug.WriteLine("[MAP_INIT] 5. Không tìm thấy Offline, dùng OSM fallback.");
                    }

                    map.Layers.Add(_routeLayer);
                    map.Layers.Add(_poiLayer);
                    map.Layers.Add(_userLayer);
                    map.Widgets.Clear();

                    if (offlineLoaded && map.Layers.FirstOrDefault() is Mapsui.Tiling.Layers.TileLayer tileLayer)
                    {
                        // Khóa xoay bản đồ
                        map.Navigator.RotationLock = true;

                        // Đã bỏ giới hạn Zoom/Pan theo yêu cầu để người dùng có thể dùng 2 lớp layer hiển thị rộng hơn.
                    }

                    _trackedNavigator = map.Navigator;
                    _trackedNavigator.ViewportChanged += OnViewportChanged;

                    _mapControl = new MapControl { Map = map };
                    _mapControl.BackgroundColor = Microsoft.Maui.Graphics.Colors.White;
                    
                    _mapControl.Info += OnMapInfo;
                    MapHost.Content = _mapControl;
                    _isMapInitialized = true;
                    Debug.WriteLine("[MAP_INIT] 7. Hoàn tất setup MapControl.");

                    // Nạp icon mới
                    _ = Task.Run(LoadIconsAsync);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[MAP_INIT] ‼️ Crash trong MainThread: {ex}");
                }
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MAP_INIT] ‼️ Lỗi InvokeOnMainThread: {ex.Message}");
        }

        if (_isMapInitialized)
        {
            Debug.WriteLine("[MAP_INIT] 8. Gọi RenderMap.");
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                RenderMap();
                FocusOnBuiVienAnchor();
            });
        }
    }

    private async Task LoadIconsAsync()
    {
        try
        {
            Debug.WriteLine("[MAP_ICONS] Loading SVG icons from package...");
            
            using var s1 = await FileSystem.OpenAppPackageFileAsync("location.svg");
            using var r1 = new StreamReader(s1);
            _defaultSvg = await r1.ReadToEndAsync();

            using var s2 = await FileSystem.OpenAppPackageFileAsync("located.svg");
            using var r2 = new StreamReader(s2);
            _selectedSvg = await r2.ReadToEndAsync();
            
            Debug.WriteLine($"[MAP_ICONS] Loaded SVG content (default={_defaultSvg?.Length}, selected={_selectedSvg?.Length})");
            
            if (!string.IsNullOrEmpty(_defaultSvg) || !string.IsNullOrEmpty(_selectedSvg))
            {
                // Bắt buộc vẽ lại bản đồ vì dữ liệu icon đã có
                _lastStaticMapKey = null;
                MainThread.BeginInvokeOnMainThread(RenderMap);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MAP_ICONS] ❌ Error loading SVG icons: {ex.Message}");
        }
    }

    private async Task InitializeRoutingAsync()
    {
        if (_isRoutingInitialized) return;
        Debug.WriteLine("[ROUTING] Initializing Itinero...");

        try
        {
            // Thử nạp hcm_route.routerdb từ Assets
            string routerDbName = "hcm_route.routerdb";
            
            using var stream = await FileSystem.OpenAppPackageFileAsync(routerDbName);
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            ms.Position = 0;

            var routerDb = RouterDb.Deserialize(ms);
            _router = new Router(routerDb);
            
            // Log all profile names to debug why they were appearing empty
            var profiles = routerDb.GetSupportedProfiles();
            foreach (var p in profiles)
            {
                Debug.WriteLine($"[ROUTING] Profile found: Name='{p.Name}', FullName='{p.FullName}'");
            }

            // More robust profile selection
            _routingProfile = profiles.FirstOrDefault(p => p.FullName.Contains("pedestrian", StringComparison.OrdinalIgnoreCase))
                           ?? profiles.FirstOrDefault(p => p.Name.Contains("pedestrian", StringComparison.OrdinalIgnoreCase))
                           ?? profiles.FirstOrDefault(p => p.FullName.Contains("car", StringComparison.OrdinalIgnoreCase))
                           ?? profiles.FirstOrDefault();

            if (_routingProfile == null)
            {
                throw new Exception("RouterDb contains no routing profiles.");
            }
            
            Debug.WriteLine($"[ROUTING] Using profile: {_routingProfile.FullName ?? _routingProfile.Name}");
            _isRoutingInitialized = true;
            Debug.WriteLine("[ROUTING] ✅ Itinero initialized successfully.");
            
            // Kích hoạt vẽ lại bản đồ nếu đã init xong
            if (_isMapInitialized)
            {
                MainThread.BeginInvokeOnMainThread(RenderMap);
            }
        }
        catch (FileNotFoundException)
        {
            Debug.WriteLine("[ROUTING] ⚠️ File hcm_route.routerdb not found. Road-network routing disabled.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ROUTING] ❌ Error initializing Itinero: {ex.Message}");
            Debug.WriteLine($"[ROUTING] StackTrace: {ex.StackTrace}");
            _isRoutingInitialized = false;
        }
    }

    // ─── Event Handlers ──────────────────────────────────────────────────────
    private async void OnStopSelected(object? sender, PoiStopItem stop)
    {
        // 1. Focus map vào stop đó
        MainThread.BeginInvokeOnMainThread(() =>
        {
            FocusOnStop(stop);

            // 2. Scroll card lên đầu
            StopsCollectionView?.ScrollTo(stop, position: ScrollToPosition.Start, animate: true);

            // 3. Tự động đẩy lên mức giữa nếu đang ở mức thấp
            if (_sheetSnapY > 0.75)
            {
                SnapToHalf();
            }
        });

        // 4. Show Audio Narration Popup
        await ShowAudioNarrationAsync(stop);
    }

    private bool _isShowingNarration = false;

    private async Task ShowAudioNarrationAsync(PoiStopItem stop)
    {
        if (_isShowingNarration) return;
        _isShowingNarration = true;

        try
        {
            // Luôn lấy ngôn ngữ mới nhất từ DB mỗi khi hiển thị popup
            var lang = await _dbService.GetSettingAsync("Language", "vi");
            Debug.WriteLine($"[TOUR_PAGE] Current app language from DB: {lang}");
            
            var narration = await _dbService.GetNarrationAsync(stop.ZoneId, lang);

            // Khởi tạo AudioPlayer với ngôn ngữ đã lưu
            AudioPlayer.Initialize(_audioService, stop, lang);
            _ = AudioPlayer.ShowAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[TOUR_PAGE] Error showing audio popup: {ex.Message}");
        }
        finally
        {
            _isShowingNarration = false;
        }
    }




    private void FocusOnStop(PoiStopItem stop)
    {
        if (!_isMapInitialized || _mapControl == null) return;

        var (cx, cy) = SphericalMercator.FromLonLat(stop.Longitude, stop.Latitude);
        var half = 50d; // zoom vào vùng 200m quanh stop
        _mapControl.Map?.Navigator?.ZoomToBox(
            new MRect(cx - half, cy - half, cx + half, cy + half),
            MBoxFit.Fit, 0, null);
        _mapControl.Refresh();
    }
    private void OnMapDataChanged(object? sender, EventArgs e)
    {
        if (!_isMapInitialized) return;

        Debug.WriteLine($"[EVENT] MapDataChanged fired, stops={_viewModel.PoiStops.Count}");

        // Reset key để RenderMap luôn vẽ lại khi data thay đổi
        _lastStaticMapKey = null;

        CancelAndDispose(ref _renderCts);
        _renderCts = new CancellationTokenSource();
        var token = _renderCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(220, token);
                if (token.IsCancellationRequested) return;
                await MainThread.InvokeOnMainThreadAsync(RenderMap);
            }
            catch (OperationCanceledException) { }
        }, token);
    }

    private void OnViewportChanged(object? sender, EventArgs e)
    {
        if (!_isMapInitialized) return;

        // Skip nếu viewport chưa thay đổi đáng kể
        var current = GetViewportExtent();
        if (current != null && _lastViewportExtent != null)
        {
            var dx = Math.Abs(current.Centroid.X - _lastViewportExtent.Centroid.X);
            var dy = Math.Abs(current.Centroid.Y - _lastViewportExtent.Centroid.Y);
            var dw = Math.Abs(current.Width - _lastViewportExtent.Width);
            if (dx < 250 && dy < 250 && dw < 500) return;
        }
        _lastViewportExtent = current;

        CancelAndDispose(ref _viewportRenderCts);
        _viewportRenderCts = new CancellationTokenSource();
        var token = _viewportRenderCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(140, token);
                if (token.IsCancellationRequested) return;
                await MainThread.InvokeOnMainThreadAsync(RenderMap);
            }
            catch (OperationCanceledException) { }
        }, token);
    }

    private void OnFocusBuiVienClicked(object? sender, EventArgs e)
    {
        FocusOnBuiVienAnchor();
    }

    // ─── Render ──────────────────────────────────────────────────────────────

    private void RenderMap()
    {
        if (!_isMapInitialized || _mapControl == null) return;

        Debug.WriteLine($"[RENDER][{DateTime.Now:HH:mm:ss.fff}] start — stops={_viewModel.PoiStops.Count}");

        if (_viewModel.PoiStops.Count == 0)
        {
            _poiLayer.Features = EmptyFeatures;
            _userLayer.Features = EmptyFeatures;
            _poiLayer.DataHasChanged();
            _userLayer.DataHasChanged();
            _lastStaticMapKey = null;
            _lastRenderedUserLocation = null;
            _mapControl.Refresh();
            return;
        }

        var selectedStop = _viewModel.PoiStops.FirstOrDefault(x => x.IsSelected);
        var orderedStops = _viewModel.PoiStops.OrderBy(x => x.OrderIndex).ToList();
        var viewportExtent = GetViewportExtent();

        // BUG FIX #1: FilterStopsByViewport trả về list mới (không dùng shared buffer)
        // → tránh mutation khi Add(selectedStop) bên dưới
        var visibleStops = FilterStopsByViewport(orderedStops, viewportExtent, 450);

        // Đảm bảo stop đang chọn luôn có trong danh sách
        if (selectedStop != null && visibleStops.All(x => x.ZoneId != selectedStop.ZoneId))
            visibleStops.Add(selectedStop);

        // Nếu không có stop nào visible → lấy tất cả (viewport chưa đúng vị trí)
        if (visibleStops.Count == 0)
            visibleStops = orderedStops;

        // Sort lại sau khi có thể đã Add
        visibleStops = visibleStops.OrderBy(x => x.OrderIndex).ToList();

        var selectedZoneId = selectedStop?.ZoneId ?? -1;

        // Key chỉ dựa vào data, không có viewport
        // → tránh vẽ lại vô tận khi user pan/zoom
        var staticMapKey = BuildStaticMapKey(visibleStops, selectedZoneId);
        var staticChanged = staticMapKey != _lastStaticMapKey;

        Debug.WriteLine($"[RENDER] visible={visibleStops.Count} selectedZone={selectedZoneId} staticChanged={staticChanged} routingInit={_isRoutingInitialized}");

        if (staticChanged)
        {
            RenderPoiLayer(visibleStops);
            RenderRouteLayer(orderedStops); // Vẽ tuyến đường nối tất cả các điểm theo lộ trình
            _lastStaticMapKey = staticMapKey;
        }
        else if (_isRoutingInitialized && (_routeLayer.Features == null || !_routeLayer.Features.Any()))
        {
            // Trường hợp user không đổi selection nhưng routing vừa init xong 
            // Vẫn cần vẽ route nếu layer đang rỗng
            RenderRouteLayer(orderedStops);
        }

        var userChanged = UpdateUserLayer();

        var didFocus = false;
        if (!_hasFocusedOnce)
        {
            FocusOnBuiVienAnchor();
            _hasFocusedOnce = true;
            _lastSelectedZoneId = selectedZoneId;
            didFocus = true;
            Debug.WriteLine("[RENDER] Initial focus → Bùi Viện");
        }
        else if (selectedZoneId != _lastSelectedZoneId)
        {
            if (selectedStop != null)
                FocusOnStop(selectedStop);
            _lastSelectedZoneId = selectedZoneId;
            didFocus = true;
            Debug.WriteLine($"[RENDER] Focus → zone={selectedZoneId}");
        }

        if (staticChanged || userChanged || didFocus)
        {
            Debug.WriteLine($"[RENDER] Refresh — static={staticChanged} user={userChanged} focus={didFocus}");
            _mapControl.Refresh();
        }
    }


    private void RenderPoiLayer(List<PoiStopItem> visibleStops)
    {
        var poiFeatures = new List<IFeature>(visibleStops.Count);

        foreach (var stop in visibleStops)
        {
            Debug.WriteLine($"[RENDER][POI] Point: {stop.Name} - Lat: {stop.Latitude}, Lon: {stop.Longitude}");
            // BUG FIX #2: destructure tuple, tạo MPoint tường minh
            var (mx, my) = SphericalMercator.FromLonLat(stop.Longitude, stop.Latitude);
            var point = new MPoint(mx, my);
            var feature = new PointFeature(point)
            {
                ["Label"] = stop.Name,
                ["Distance"] = stop.DistanceText,
                ["ZoneId"] = stop.ZoneId
            };

            IStyle iconStyle;

        if (!string.IsNullOrEmpty(_defaultSvg) && !string.IsNullOrEmpty(_selectedSvg))
        {
            // CHỈ DÙNG SVG
            iconStyle = new ImageStyle
            {
                Image = "svg-content://" + (stop.IsSelected ? _selectedSvg : _defaultSvg),
                SymbolScale = stop.IsSelected ? 1.4 : 1.1,
                Offset = new Offset(0, 0)
            };
        }
        else
        {
            // CHỈ DÙNG CHẤM TRÒN KHI SVG LỖI
            iconStyle = new SymbolStyle
            {
                SymbolType = SymbolType.Ellipse,
                Fill = new Mapsui.Styles.Brush(stop.IsSelected 
                    ? Mapsui.Styles.Color.FromArgb(255, 67, 160, 71) 
                    : Mapsui.Styles.Color.FromArgb(255, 255, 75, 75)),
                Outline = new Pen(Mapsui.Styles.Color.Transparent, 0), // Dùng trong suốt 0px thay vì null
                
                SymbolScale = 0,
                Opacity = 0
            };
        }

        // Làm sạch style cũ trước khi add mới
        feature.Styles.Clear();

        // Chỉ add MỘT Style đại diện cho Icon/Chấm
        feature.Styles.Add(iconStyle);

            feature.Styles.Add(new LabelStyle
            {
                Text = stop.Name,
                MaxVisible = 2.0, // Only show if zoomed in (resolution < 2.0)
                CollisionDetection = true, // Hide if overlapping with other labels
                // Ép halo về hoàn toàn vô hình
                Halo = new Mapsui.Styles.Pen(Mapsui.Styles.Color.Transparent, 0),
                BackColor = null, // Thử để null thay vì Transparent Brush
                ForeColor = Mapsui.Styles.Color.Black,
                HorizontalAlignment = LabelStyle.HorizontalAlignmentEnum.Left,
                VerticalAlignment = LabelStyle.VerticalAlignmentEnum.Center,
                Offset = new Offset(15, 0), // GG Map style: to the right
                Font = new Mapsui.Styles.Font { Size = 11, Bold = true }
            });

            poiFeatures.Add(feature);
        }

        _poiLayer.Features = poiFeatures;
        _poiLayer.DataHasChanged();
        Debug.WriteLine($"[RENDER][POI] {poiFeatures.Count} stops");
    }

    private void RenderRouteLayer(List<PoiStopItem> allStops)
    {
        if (!_isRoutingInitialized || _router == null || _routingProfile == null || allStops.Count < 2)
        {
            _routeLayer.Features = EmptyFeatures;
            _routeLayer.DataHasChanged();
            return;
        }

        var routeFeatures = new List<IFeature>();
        Debug.WriteLine($"[RENDER][ROUTE] Calculating PEDESTRIAN route for {allStops.Count} stops...");
        int successCount = 0;

        try
        {
            for (int i = 0; i < allStops.Count - 1; i++)
            {
                var start = allStops[i];
                var end = allStops[i + 1];

                // Calculate straight-line distance in meters for comparison
                var (sx, sy) = SphericalMercator.FromLonLat(start.Longitude, start.Latitude);
                var (ex, ey) = SphericalMercator.FromLonLat(end.Longitude, end.Latitude);
                double directDistance = Math.Sqrt(Math.Pow(ex - sx, 2) + Math.Pow(ey - sy, 2));

                // Resolve points on road network (1000m radius)
                var p1 = _router.Resolve(_routingProfile, (float)start.Latitude, (float)start.Longitude, 1000);
                var p2 = _router.Resolve(_routingProfile, (float)end.Latitude, (float)end.Longitude, 1000);

                Route? route = null;
                bool useStraightLine = false;

                if (p1.EdgeId == uint.MaxValue || p2.EdgeId == uint.MaxValue)
                {
                    Debug.WriteLine($"[RENDER][ROUTE] ⚠️ Points for segment {i + 1} too far from road. Falling back to straight line.");
                    useStraightLine = true;
                }
                else
                {
                    var routeResult = _router.TryCalculate(_routingProfile, p1, p2);
                    if (!routeResult.IsError)
                    {
                        route = routeResult.Value;
                        // If the route distance is more than 3x the direct distance, it's likely an OSM detour/one-way issue
                        if (route.TotalDistance > directDistance * 3)
                        {
                            Debug.WriteLine($"[RENDER][ROUTE] ℹ️ Segment {i + 1} detour detected ({route.TotalDistance:F0}m vs {directDistance:F0}m). Using straight line.");
                            useStraightLine = true;
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"[RENDER][ROUTE] ℹ️ Segment {i + 1} routing failed ({routeResult.ErrorMessage}). Using straight line.");
                        useStraightLine = true;
                    }
                }

                if (useStraightLine)
                {
                    var line = new LineString(new[] { 
                        new NetTopologySuite.Geometries.Coordinate(sx, sy), 
                        new NetTopologySuite.Geometries.Coordinate(ex, ey) 
                    });
                    routeFeatures.Add(new GeometryFeature(line));
                    successCount++;
                }
                else if (route?.Shape != null)
                {
                    var coordinates = route.Shape.Select(p => {
                        var (nx, ny) = SphericalMercator.FromLonLat(p.Longitude, p.Latitude);
                        return new NetTopologySuite.Geometries.Coordinate(nx, ny);
                    }).ToArray();

                    routeFeatures.Add(new GeometryFeature(new LineString(coordinates)));
                    successCount++;
                    Debug.WriteLine($"[RENDER][ROUTE] ✅ Segment {i + 1} ({start.Name} -> {end.Name}): {route.TotalDistance:F0}m");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[RENDER][ROUTE] ❌ Critical error: {ex.Message}");
        }

        _routeLayer.Features = routeFeatures;
        _routeLayer.DataHasChanged();
        Debug.WriteLine($"[RENDER][ROUTE] Finished. {successCount}/{allStops.Count - 1} segments rendered.");
    }

    private bool UpdateUserLayer()
    {
        var userLocation = _viewModel.UserLocation;

        if (userLocation == null)
        {
            if (_userLayer.Features != null)
            {
                using var enumerator = _userLayer.Features.GetEnumerator();
                if (!enumerator.MoveNext()) return false;
            }
            _userLayer.Features = EmptyFeatures;
            _userLayer.DataHasChanged();
            _lastRenderedUserLocation = null;
            return true;
        }

        var current = (userLocation.Latitude, userLocation.Longitude);

        if (_lastRenderedUserLocation is { } last)
        {
            var moved = Microsoft.Maui.Devices.Sensors.Location.CalculateDistance(
                new Microsoft.Maui.Devices.Sensors.Location(last.Latitude, last.Longitude),
                new Microsoft.Maui.Devices.Sensors.Location(current.Latitude, current.Longitude),
                Microsoft.Maui.Devices.Sensors.DistanceUnits.Kilometers) * 1000d;

            if (moved < 8) return false;
        }

        var (ux, uy) = SphericalMercator.FromLonLat(current.Longitude, current.Latitude);
        var userFeature = new PointFeature(new MPoint(ux, uy));
        userFeature.Styles.Add(new SymbolStyle
        {
            SymbolType = SymbolType.Ellipse,
            Fill = new Mapsui.Styles.Brush(Mapsui.Styles.Color.FromArgb(255, 33, 150, 243)),
            Outline = new Pen(Mapsui.Styles.Color.White, 2),
            SymbolScale = 1.0
        });

        _userLayer.Features = new List<IFeature> { userFeature };
        _userLayer.DataHasChanged();
        _lastRenderedUserLocation = current;
        return true;
    }

    // ─── Focus / Viewport ────────────────────────────────────────────────────

    private void FocusOnSelectedStop()
    {
        if (!_isMapInitialized || _mapControl == null) return;
        if (_viewModel.PoiStops.Count == 0) return;
        MoveToFitRegion(_viewModel.PoiStops.FirstOrDefault(x => x.IsSelected));
        _mapControl.Refresh();
    }

    private void FocusOnBuiVienAnchor()
    {
        if (!_isMapInitialized || _mapControl == null) return;

        var (cx, cy) = SphericalMercator.FromLonLat(BuiVienLongitude, BuiVienLatitude);
        var half = MinFocusMeters / 2d;
        _mapControl.Map?.Navigator?.ZoomToBox(
            new MRect(cx - half, cy - half, cx + half, cy + half),
            MBoxFit.Fit, 0, null);
        _mapControl.Refresh();
    }

    private void MoveToFitRegion(PoiStopItem? _)
    {
        if (!_isMapInitialized || _mapControl == null) return;

        // BUG FIX #2: destructure rõ ràng, dùng MPoint(x,y)
        var points = _viewModel.PoiStops
            .Select(s =>
            {
                var (x, y) = SphericalMercator.FromLonLat(s.Longitude, s.Latitude);
                return new MPoint(x, y);
            })
            .ToList();

        if (points.Count == 0)
        {
            var (cx, cy) = SphericalMercator.FromLonLat(BuiVienLongitude, BuiVienLatitude);
            var half = MinFocusMeters / 2d;
            _mapControl.Map?.Navigator?.ZoomToBox(
                new MRect(cx - half, cy - half, cx + half, cy + half),
                MBoxFit.Fit, 0, null);
            return;
        }

        var minX = points.Min(p => p.X);
        var maxX = points.Max(p => p.X);
        var minY = points.Min(p => p.Y);
        var maxY = points.Max(p => p.Y);

        var padX = Math.Max((maxX - minX) * 0.35, MinFocusMeters / 2d);
        var padY = Math.Max((maxY - minY) * 0.35, MinFocusMeters / 2d);

        _mapControl.Map?.Navigator?.ZoomToBox(
            new MRect(minX - padX, minY - padY, maxX + padX, maxY + padY),
            MBoxFit.Fit, 0, null);
    }

    private MRect? GetViewportExtent()
    {
        // Viewport là struct — không dùng ?. được
        var navigator = _mapControl?.Map?.Navigator;
        if (navigator == null) return null;
        return navigator.Viewport.ToExtent();
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static void CancelAndDispose(ref CancellationTokenSource? cts)
    {
        try { cts?.Cancel(); } catch { }
        cts?.Dispose();
        cts = null;
    }

    private string BuildStaticMapKey(IReadOnlyList<PoiStopItem> stops, int selectedZoneId)
        => $"{_isRoutingInitialized}|{selectedZoneId}|{string.Join(';', stops.Select(s =>
            $"{s.ZoneId}:{s.OrderIndex}:{s.Latitude:0.000000}:{s.Longitude:0.000000}"))}";

    // BUG FIX #1: trả về List MỚI thay vì shared buffer
    // → tránh mutation khi caller gọi visibleStops.Add() / .OrderBy()
    private static List<PoiStopItem> FilterStopsByViewport(
        IEnumerable<PoiStopItem> source,
        MRect? viewportExtent,
        double paddingMeters)
    {
        if (viewportExtent == null)
            return source.ToList(); // bản sao mới

        var padded = new MRect(
            viewportExtent.MinX - paddingMeters,
            viewportExtent.MinY - paddingMeters,
            viewportExtent.MaxX + paddingMeters,
            viewportExtent.MaxY + paddingMeters);

        var result = new List<PoiStopItem>();
        foreach (var stop in source)
        {
            var (sx, sy) = SphericalMercator.FromLonLat(stop.Longitude, stop.Latitude);
            if (padded.Contains(sx, sy))
                result.Add(stop);
        }
        return result;
    }
    private void OnMapInfo(object? sender, Mapsui.MapInfoEventArgs e)
    {
        // Trong Mapsui 5, GetMapInfo yêu cầu danh sách layer để query (thay cho IsMapInfoLayer)
        var mapInfo = e.GetMapInfo(_mapControl?.Map?.Layers);
        if (mapInfo?.Feature == null) return;

        // Tìm feature thuộc POI layer
        if (mapInfo.Layer?.Name != "PoiLayer") return;

        var feature = mapInfo.Feature;
        
        // Cách 1: Tìm theo ZoneId (ổn định nhất)
        if (feature["ZoneId"] is int zoneId)
        {
            var stopById = _viewModel.PoiStops.FirstOrDefault(s => s.ZoneId == zoneId);
            if (stopById != null)
            {
                HandleStopSelection(stopById);
                return;
            }
        }

        // Cách 2: Fallback tìm theo label (nếu ZoneId null)
        var label = feature["Label"]?.ToString();
        if (string.IsNullOrEmpty(label)) return;

        var stopByLabel = _viewModel.PoiStops.FirstOrDefault(s => s.Name == label);
        if (stopByLabel == null) return;

        HandleStopSelection(stopByLabel);
    }

    private void HandleStopSelection(PoiStopItem stop)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            // Select stop trong ViewModel
            _viewModel.SelectStopByZoneId(stop.ZoneId);

            // Focus map
            FocusOnStop(stop);

            // Scroll card lên đầu
            StopsCollectionView?.ScrollTo(stop, position: ScrollToPosition.Start, animate: true);
        });
    }
}
