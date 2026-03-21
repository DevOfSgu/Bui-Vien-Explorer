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

namespace TravelSystem.Mobile.Views;

public partial class TourDetailPage : ContentPage
{
    private const double BuiVienLatitude = 10.764017;
    private const double BuiVienLongitude = 106.692527;
    private const double MinFocusMeters = 550;

    // Mapsui layers
    private readonly MemoryLayer _routeLayer = new() { Name = "RouteLayer" };
    private readonly MemoryLayer _poiLayer  = new() { Name = "PoiLayer" };
    private readonly MemoryLayer _userLayer = new() { Name = "UserLayer" };

    private readonly TourDetailViewModel _viewModel;
    private MapControl? _mapControl;
    private Mapsui.Navigator? _trackedNavigator;

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
    private static readonly double[] SnapPoints = { 0.10, 0.52, 0.88 };

    private double _sheetSnapY     = 0.52;  // snap hiện tại (bắt đầu ở Half)
    private double _panStartTransY = 0;     // TranslationY lúc bắt đầu kéo
    private double _lastPanTotalY  = 0;     // TotalY tích luỹ gesture hiện tại
    private bool   _isPanning      = false;

    public TourDetailPage(TourDetailViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
        Shell.SetTabBarIsVisible(this, false);
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

    // ─── Lifecycle ───────────────────────────────────────────────────────────

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Shell.SetTabBarIsVisible(this, false);

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
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.StopForegroundTracking();
        _viewModel.MapDataChanged -= OnMapDataChanged;

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

        await Task.Delay(100); // nhường frame để MAUI layout xong

        Debug.WriteLine($"[MAP_INIT][{DateTime.Now:HH:mm:ss.fff}] START");

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (_isMapInitialized) return;

            var map = new Mapsui.Map();
            // Fix double projection: khai báo rõ map dùng EPSG:3857
            // → Mapsui biết features đã ở Mercator, không convert thêm
            map.CRS = "EPSG:3857";
            map.Layers.Add(OpenStreetMap.CreateTileLayer());
            map.Layers.Add(_routeLayer);
            map.Layers.Add(_poiLayer);
            map.Layers.Add(_userLayer);
            map.Widgets.Clear();

            _trackedNavigator = map.Navigator;
            _trackedNavigator.ViewportChanged += OnViewportChanged;

            _mapControl = new MapControl { Map = map };
            MapHost.Content = _mapControl;
            _isMapInitialized = true;

            Debug.WriteLine($"[MAP_INIT][{DateTime.Now:HH:mm:ss.fff}] Map ready — {map.Layers.Count} layers");
        });

        // Sau khi _isMapInitialized = true mới render
        // → đảm bảo dù StartForegroundTracking load data từ trước, vẫn render được
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            Debug.WriteLine($"[MAP_INIT] Calling initial RenderMap, stops={_viewModel.PoiStops.Count}");
            RenderMap();
            FocusOnBuiVienAnchor();
        });
    }

    // ─── Event Handlers ──────────────────────────────────────────────────────

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
            _routeLayer.Features = EmptyFeatures;
            _poiLayer.Features = EmptyFeatures;
            _userLayer.Features = EmptyFeatures;
            _routeLayer.DataHasChanged();
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

        Debug.WriteLine($"[RENDER] visible={visibleStops.Count} selectedZone={selectedZoneId} staticChanged={staticChanged}");

        if (staticChanged)
        {
            RenderRouteLayer(visibleStops);
            RenderPoiLayer(visibleStops);
            _lastStaticMapKey = staticMapKey;
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
            MoveToFitRegion(selectedStop);
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

    private void RenderRouteLayer(List<PoiStopItem> visibleStops)
    {
        if (visibleStops.Count < 2)
        {
            _routeLayer.Features = EmptyFeatures;
            _routeLayer.DataHasChanged();
            Debug.WriteLine("[RENDER][Route] < 2 stops → skip");
            return;
        }

        // BUG FIX #2: destructure tuple rõ ràng, dùng new MPoint(x,y)
        // Tránh nhầm lẫn giữa tuple .x (thường) và MPoint .X (hoa)
        var routeCoords = visibleStops
            .Select(s =>
            {
                Debug.WriteLine($"[RENDER][Route] Line point: {s.Name} - Lat: {s.Latitude}, Lon: {s.Longitude}");
                var (mx, my) = SphericalMercator.FromLonLat(s.Longitude, s.Latitude);
                return new Coordinate(mx, my);
            })
            .ToArray();

        var routeFeature = new LineString(routeCoords).ToFeature();

        // Mỗi feature cần style instance riêng — không share static
        routeFeature.Styles.Add(new VectorStyle
        {
            Line = new Pen(Mapsui.Styles.Color.FromArgb(255, 59, 130, 246), 4)
        });

        _routeLayer.Features = new List<IFeature> { routeFeature };
        _routeLayer.DataHasChanged();
        Debug.WriteLine($"[RENDER][Route] {routeCoords.Length} points");
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
                ["Label"] = $"{stop.OrderIndex}. {stop.Name}",
                ["Distance"] = stop.DistanceText
            };

            // Mỗi feature có style riêng
            feature.Styles.Add(stop.IsSelected
                ? new SymbolStyle
                {
                    SymbolType = SymbolType.Ellipse,
                    Fill = new Mapsui.Styles.Brush(Mapsui.Styles.Color.FromArgb(255, 67, 160, 71)),
                    Outline = new Pen(Mapsui.Styles.Color.White, 2),
                    SymbolScale = 0.9
                }
                : new SymbolStyle
                {
                    SymbolType = SymbolType.Ellipse,
                    Fill = new Mapsui.Styles.Brush(Mapsui.Styles.Color.FromArgb(255, 255, 75, 75)),
                    Outline = new Pen(Mapsui.Styles.Color.White, 2),
                    SymbolScale = 0.75
                });

            feature.Styles.Add(new LabelStyle
            {
                Text = $"{stop.OrderIndex}. {stop.Name}",
                Halo = new Pen(Mapsui.Styles.Color.White, 2),
                BackColor = new Mapsui.Styles.Brush(Mapsui.Styles.Color.Transparent),
                ForeColor = Mapsui.Styles.Color.Black,
                Offset = new Offset(0, 16),
                Font = new Mapsui.Styles.Font { Size = 12, Bold = true }
            });

            poiFeatures.Add(feature);
        }

        _poiLayer.Features = poiFeatures;
        _poiLayer.DataHasChanged();
        Debug.WriteLine($"[RENDER][POI] {poiFeatures.Count} stops");
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

    private static string BuildStaticMapKey(IReadOnlyList<PoiStopItem> stops, int selectedZoneId)
        => $"{selectedZoneId}|{string.Join(';', stops.Select(s =>
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
}
