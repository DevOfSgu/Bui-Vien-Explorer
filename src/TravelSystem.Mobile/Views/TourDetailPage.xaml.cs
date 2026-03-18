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
    private const double BuiVienLatitude = 10.7677;
    private const double BuiVienLongitude = 106.6932;
    private const double MinFocusMeters = 600;
    private const double MaxFocusMeters = 2000;

    private readonly TourDetailViewModel _viewModel;
    private MapControl? _mapControl;
    private readonly MemoryLayer _routeLayer = new() { Name = "RouteLayer", IsMapInfoLayer = true };
    private readonly MemoryLayer _poiLayer = new() { Name = "PoiLayer", IsMapInfoLayer = true };
    private readonly MemoryLayer _userLayer = new() { Name = "UserLayer", IsMapInfoLayer = true };
    private CancellationTokenSource? _renderCts;
    private CancellationTokenSource? _startupFocusCts;
    private bool _hasFocusedBuiVien;
    private int _lastSelectedZoneId = -1;
    private string? _lastStaticMapKey;
    private (double Latitude, double Longitude)? _lastRenderedUserLocation;
    private CancellationTokenSource? _viewportRenderCts;
    private bool _isMapInitialized;
    private Task? _mapInitTask;

    public TourDetailPage(TourDetailViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;

        Shell.SetTabBarIsVisible(this, false);
        _ = Task.Run(PreWarmRouteRendering);
    }

    private static void PreWarmRouteRendering()
    {
        var warmLine = new LineString([new Coordinate(0, 0), new Coordinate(1, 1)]);
        _ = warmLine.ToFeature();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.StopForegroundTracking();
        _viewModel.MapDataChanged -= OnMapDataChanged;
        if (_mapControl?.Map?.Navigator != null)
        {
            _mapControl.Map.Navigator.ViewportChanged -= OnViewportChanged;
        }
        try { _renderCts?.Cancel(); } catch { }
        _renderCts?.Dispose();
        _renderCts = null;

        try { _viewportRenderCts?.Cancel(); } catch { }
        _viewportRenderCts?.Dispose();
        _viewportRenderCts = null;

        try { _startupFocusCts?.Cancel(); } catch { }
        _startupFocusCts?.Dispose();
        _startupFocusCts = null;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Shell.SetTabBarIsVisible(this, false);
        _viewModel.MapDataChanged -= OnMapDataChanged;
        _viewModel.MapDataChanged += OnMapDataChanged;
        _viewModel.StartForegroundTracking();
        _mapInitTask ??= InitializeMapDeferredAsync();
        ScheduleStartupFocus();
    }

    private async Task InitializeMapDeferredAsync()
    {
        await Task.Delay(150);
        await InitializeMapAsync();
    }

    private async Task InitializeMapAsync()
    {
        if (_isMapInitialized)
            return;

        Debug.WriteLine($"[MAP_INIT][{DateTime.Now:HH:mm:ss.fff}] START");
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (_isMapInitialized)
                return;

            // Trong Mapsui 5, MapControl.Map = null ngay sau new MapControl()
            // → tạo Map riêng, cấu hình layers xong rồi mới gán vào MapControl
            var map = new Mapsui.Map();

            map.Layers.Add(OpenStreetMap.CreateTileLayer());
            Debug.WriteLine($"[MAP_INIT][{DateTime.Now:HH:mm:ss.fff}] OSM TileLayer added");

            map.Layers.Add(_routeLayer);
            map.Layers.Add(_poiLayer);
            map.Layers.Add(_userLayer);
            map.Widgets.Clear();

            map.Navigator.ViewportChanged -= OnViewportChanged;
            map.Navigator.ViewportChanged += OnViewportChanged;

            _mapControl = new MapControl { Map = map };
            MapHost.Content = _mapControl;
            _isMapInitialized = true;
            Debug.WriteLine($"[MAP_INIT][{DateTime.Now:HH:mm:ss.fff}] Map configured: {map.Layers.Count} layers — COMPLETE");
        });

        Debug.WriteLine($"[MAP_INIT][{DateTime.Now:HH:mm:ss.fff}] Calling RenderMap");
        await MainThread.InvokeOnMainThreadAsync(RenderMap);
        Debug.WriteLine($"[MAP_INIT][{DateTime.Now:HH:mm:ss.fff}] RenderMap done");
    }

    private void OnMapDataChanged(object? sender, EventArgs e)
    {
        if (!_isMapInitialized)
            return;

        try { _renderCts?.Cancel(); } catch { }
        _renderCts?.Dispose();
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
            catch (OperationCanceledException)
            {
            }
        }, token);

    }

    private void OnViewportChanged(object? sender, EventArgs e)
    {
        if (!_isMapInitialized)
            return;

        try { _viewportRenderCts?.Cancel(); } catch { }
        _viewportRenderCts?.Dispose();
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
            catch (OperationCanceledException)
            {
            }
        }, token);
    }

    private void ScheduleStartupFocus()
    {
        try { _startupFocusCts?.Cancel(); } catch { }
        _startupFocusCts?.Dispose();
        _startupFocusCts = new CancellationTokenSource();
        var token = _startupFocusCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(180, token);
                if (token.IsCancellationRequested) return;

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    if (!_isMapInitialized || _mapControl == null) return;
                    if (_viewModel.PoiStops.Count == 0) return;

                    var selectedStop = _viewModel.PoiStops.FirstOrDefault(x => x.IsSelected);
                    MoveToFitRegion(selectedStop);
                    _mapControl.Refresh();
                });
            }
            catch (OperationCanceledException)
            {
            }
        }, token);
    }

    private void RenderMap()
    {
        if (!_isMapInitialized || _mapControl == null)
            return;

        if (_viewModel.PoiStops.Count == 0)
        {
            _routeLayer.Features = null;
            _poiLayer.Features = null;
            _userLayer.Features = null;
            _lastStaticMapKey = null;
            _lastRenderedUserLocation = null;
            _mapControl.Refresh();
            return;
        }

        var selectedStop = _viewModel.PoiStops.FirstOrDefault(x => x.IsSelected);
        var orderedStops = _viewModel.PoiStops.OrderBy(x => x.OrderIndex).ToList();
        var viewportExtent = GetViewportExtent();
        var visibleStops = FilterStopsByViewport(orderedStops, viewportExtent, 450);

        if (selectedStop != null && visibleStops.All(x => x.ZoneId != selectedStop.ZoneId))
        {
            visibleStops.Add(selectedStop);
            visibleStops = visibleStops.OrderBy(x => x.OrderIndex).ToList();
        }

        if (visibleStops.Count == 0)
        {
            visibleStops = orderedStops.Take(12).ToList();
        }

        var selectedZoneId = selectedStop?.ZoneId ?? -1;
        var staticMapKey = BuildStaticMapKey(visibleStops, selectedZoneId, viewportExtent);

        var staticChanged = staticMapKey != _lastStaticMapKey;
        var userChanged = UpdateUserLayer();
        var didFocus = false;

        if (staticChanged)
        {
            _routeLayer.Features = null;
            _poiLayer.Features = null;

            if (visibleStops.Count >= 2)
            {
                var routePoints = visibleStops
                    .Select(stop => SphericalMercator.FromLonLat(stop.Longitude, stop.Latitude).ToMPoint())
                    .ToList();

                var routeLine = new LineString(routePoints.Select(p => new Coordinate(p.X, p.Y)).ToArray());
                var routeFeature = routeLine.ToFeature();

                routeFeature.Styles.Add(new VectorStyle
                {
                    Line = new Pen(Mapsui.Styles.Color.FromArgb(255, 59, 130, 246), 4)
                });

                _routeLayer.Features = [routeFeature];
            }

            var poiFeatures = new List<IFeature>();

            foreach (var stop in visibleStops)
            {
                var point = SphericalMercator.FromLonLat(stop.Longitude, stop.Latitude).ToMPoint();

                var feature = new PointFeature(point)
                {
                    ["Label"] = $"{stop.OrderIndex}. {stop.Name}",
                    ["Distance"] = stop.DistanceText
                };
                feature.Styles.Add(new SymbolStyle
                {
                    SymbolType = SymbolType.Ellipse,
                    Fill = new Mapsui.Styles.Brush(stop.IsSelected ? Mapsui.Styles.Color.FromArgb(255, 67, 160, 71) : Mapsui.Styles.Color.FromArgb(255, 255, 75, 75)),
                    Outline = new Pen(Mapsui.Styles.Color.White, 2),
                    SymbolScale = stop.IsSelected ? 0.9 : 0.75
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
            _routeLayer.DataHasChanged();
            _lastStaticMapKey = staticMapKey;
        }

        if (staticChanged && (!_hasFocusedBuiVien || selectedZoneId != _lastSelectedZoneId))
        {
            MoveToFitRegion(selectedStop);
            _hasFocusedBuiVien = true;
            _lastSelectedZoneId = selectedZoneId;
            didFocus = true;
        }

        if (staticChanged || userChanged || didFocus)
        {
            _mapControl.Refresh();
        }
    }

    private bool UpdateUserLayer()
    {
        var userLocation = _viewModel.UserLocation;

        if (userLocation == null)
        {
            if (_userLayer.Features != null)
            {
                _userLayer.Features = null;
                _lastRenderedUserLocation = null;
                return true;
            }

            return false;
        }

        var current = (userLocation.Latitude, userLocation.Longitude);
        if (_lastRenderedUserLocation is { } last)
        {
            var movedMeters = Microsoft.Maui.Devices.Sensors.Location.CalculateDistance(
                new Microsoft.Maui.Devices.Sensors.Location(last.Latitude, last.Longitude),
                new Microsoft.Maui.Devices.Sensors.Location(current.Latitude, current.Longitude),
                Microsoft.Maui.Devices.Sensors.DistanceUnits.Kilometers) * 1000d;

            if (movedMeters < 8)
            {
                return false;
            }
        }

        var userPoint = SphericalMercator.FromLonLat(current.Longitude, current.Latitude).ToMPoint();
        var userFeature = new PointFeature(userPoint);
        userFeature.Styles.Add(new SymbolStyle
        {
            SymbolType = SymbolType.Ellipse,
            Fill = new Mapsui.Styles.Brush(Mapsui.Styles.Color.FromArgb(255, 33, 150, 243)),
            Outline = new Pen(Mapsui.Styles.Color.White, 2),
            SymbolScale = 1.0
        });
        _userLayer.Features = [userFeature];
        _userLayer.DataHasChanged();
        _lastRenderedUserLocation = current;
        return true;
    }

    private static string BuildStaticMapKey(IReadOnlyList<PoiStopItem> orderedStops, int selectedZoneId)
    {
        return $"{selectedZoneId}|{string.Join(';', orderedStops.Select(s => $"{s.ZoneId}:{s.OrderIndex}:{s.Latitude:0.000000}:{s.Longitude:0.000000}"))}";
    }

    private static string BuildStaticMapKey(IReadOnlyList<PoiStopItem> orderedStops, int selectedZoneId, MRect? viewportExtent)
    {
        if (viewportExtent == null)
        {
            return BuildStaticMapKey(orderedStops, selectedZoneId);
        }

        var vpKey = $"{Math.Round(viewportExtent.MinX / 250d)}:{Math.Round(viewportExtent.MinY / 250d)}:{Math.Round(viewportExtent.MaxX / 250d)}:{Math.Round(viewportExtent.MaxY / 250d)}";
        return $"{BuildStaticMapKey(orderedStops, selectedZoneId)}|{vpKey}";
    }

    private MRect? GetViewportExtent()
    {
        var viewport = _mapControl?.Map?.Navigator?.Viewport;
        return viewport?.ToExtent();
    }

    private static List<PoiStopItem> FilterStopsByViewport(IEnumerable<PoiStopItem> source, MRect? viewportExtent, double paddingMeters)
    {
        if (viewportExtent == null)
        {
            return source.ToList();
        }

        var padded = new MRect(
            viewportExtent.MinX - paddingMeters,
            viewportExtent.MinY - paddingMeters,
            viewportExtent.MaxX + paddingMeters,
            viewportExtent.MaxY + paddingMeters);

        return source.Where(stop =>
        {
            var point = SphericalMercator.FromLonLat(stop.Longitude, stop.Latitude).ToMPoint();
            return padded.Contains(point.X, point.Y);
        }).ToList();
    }

    private void MoveToFitRegion(PoiStopItem? selectedStop)
    {
        if (!_isMapInitialized || _mapControl == null)
            return;

        var points = _viewModel.PoiStops
            .Select(x => SphericalMercator.FromLonLat(x.Longitude, x.Latitude).ToMPoint())
            .ToList();

        var buiVienCenter = SphericalMercator.FromLonLat(BuiVienLongitude, BuiVienLatitude).ToMPoint();

        if (points.Count == 0)
        {
            var fallbackHalf = MinFocusMeters / 2d;
            _mapControl.Map?.Navigator?.ZoomToBox(
                new MRect(buiVienCenter.X - fallbackHalf, buiVienCenter.Y - fallbackHalf,
                          buiVienCenter.X + fallbackHalf, buiVienCenter.Y + fallbackHalf),
                MBoxFit.Fit, 0, null);
            return;
        }

        // Tính bounding box thật sự của tất cả stops
        var minX = points.Min(p => p.X);
        var maxX = points.Max(p => p.X);
        var minY = points.Min(p => p.Y);
        var maxY = points.Max(p => p.Y);

        // Padding 30% mỗi phía để các POI không sát mép
        var bboxWidth = maxX - minX;
        var bboxHeight = maxY - minY;
        var padX = Math.Max(bboxWidth * 0.35, MinFocusMeters / 2d);
        var padY = Math.Max(bboxHeight * 0.35, MinFocusMeters / 2d);

        var bbox = new MRect(
            minX - padX,
            minY - padY,
            maxX + padX,
            maxY + padY);

        _mapControl.Map?.Navigator?.ZoomToBox(bbox, MBoxFit.Fit, 0, null);
    }

    private void OnFocusBuiVienClicked(object? sender, EventArgs e)
    {
        if (!_isMapInitialized || _mapControl == null)
            return;

        var selectedStop = _viewModel.PoiStops.FirstOrDefault(x => x.IsSelected);
        MoveToFitRegion(selectedStop);
        _mapControl.Refresh();
    }

}
