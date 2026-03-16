using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Graphics;
using System.Diagnostics;
using System.Collections.ObjectModel;
using TravelSystem.Mobile.Services;

namespace TravelSystem.Mobile.ViewModels;

public partial class MainPageViewModel : ObservableObject
{
    private readonly ApiService _apiService;
    private readonly DatabaseService _databaseService;

    public ObservableCollection<RouteCardItem> RouteCards { get; } = [];

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _errorMessage = string.Empty;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public string ToursAvailableText => $"{RouteCards.Count} tours available";

    partial void OnErrorMessageChanged(string value)
    {
        OnPropertyChanged(nameof(HasError));
    }

    public MainPageViewModel(ApiService apiService, DatabaseService databaseService)
    {
        _apiService = apiService;
        _databaseService = databaseService;
    }

    [RelayCommand]
    private async Task LoadData()
    {
        if (IsLoading) return;

        try
        {
            IsLoading = true;
            ErrorMessage = string.Empty;

            Debug.WriteLine("[MAIN_VM] Initializing local database...");
            await _databaseService.InitializeAsync();
            Debug.WriteLine("[MAIN_VM] Local database initialization completed.");

            await LoadRouteCardsFromLocalAsync();

            var lastSyncedAt = await _databaseService.GetSettingAsync("LastSyncedAt", string.Empty);
            Debug.WriteLine($"[MAIN_VM] LastSyncedAt = {(string.IsNullOrWhiteSpace(lastSyncedAt) ? "<empty>" : lastSyncedAt)}");
            try
            {
                using var syncCts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                Debug.WriteLine("[MAIN_VM] Checking server updates...");
                var syncData = await _apiService.GetRouteSyncDataAsync(lastSyncedAt, syncCts.Token);
                if (syncData is not null)
                {
                    Debug.WriteLine($"[MAIN_VM] Server has updates. Routes={syncData.Routes.Count}, Zones={syncData.Zones.Count}, Narrations={syncData.Narrations.Count}, Timestamp={syncData.Timestamp:o}");
                    await _databaseService.SaveRouteSyncDataAsync(syncData);
                    Debug.WriteLine("[MAIN_VM] Local database updated from server.");
                    await LoadRouteCardsFromLocalAsync();
                }
                else
                {
                    Debug.WriteLine("[MAIN_VM] No new updates from server. Using local cache.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SYNC] Using local cache because sync failed: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Không thể tải dữ liệu: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadRouteCardsFromLocalAsync()
    {
        var routeSummaries = await _databaseService.GetRouteSummariesAsync();
        Debug.WriteLine($"[MAIN_VM] Loaded {routeSummaries.Count} routes from local database.");

        RouteCards.Clear();
        foreach (var item in routeSummaries)
        {
            RouteCards.Add(RouteCardItem.FromSummary(item));
        }

        OnPropertyChanged(nameof(ToursAvailableText));

        if (RouteCards.Count == 0)
        {
            ErrorMessage = "Không có tour để hiển thị.";
        }
        else
        {
            ErrorMessage = string.Empty;
        }
    }
}

public sealed class RouteCardItem
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string StopsText { get; init; } = string.Empty;
    public string MinutesText { get; init; } = string.Empty;
    public string ImageUrl { get; init; } = string.Empty;
    public string IconGlyph { get; init; } = "🚶";
    public Color IconBackgroundColor { get; init; } = Color.FromArgb("#F3F4F6");

    public static RouteCardItem FromSummary(RouteSummary summary)
    {
        var (icon, color) = summary.ZoneType switch
        {
            1 => ("🍜", Color.FromArgb("#FFF1E2")),
            2 => ("🍸", Color.FromArgb("#EFE7FF")),
            3 => ("🏛️", Color.FromArgb("#E8EEFF")),
            _ => ("🚶", Color.FromArgb("#FFE9E9"))
        };

        return new RouteCardItem
        {
            Name = summary.Name,
            Description = string.IsNullOrWhiteSpace(summary.Description) ? "Explore the heart of Saigon" : summary.Description,
            StopsText = $"{summary.StopCount} điểm dừng",
            MinutesText = $"{summary.DurationMinutes} phút",
            ImageUrl = summary.ImageUrl,
            IconGlyph = icon,
            IconBackgroundColor = color
        };
    }
}
