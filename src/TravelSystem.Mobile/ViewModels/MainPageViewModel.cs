using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Graphics;
using System.Collections.ObjectModel;
using TravelSystem.Mobile.Services;

namespace TravelSystem.Mobile.ViewModels;

public partial class MainPageViewModel : ObservableObject
{
    private readonly ApiService _apiService;

    public ObservableCollection<RouteCardItem> RouteCards { get; } = [];

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _errorMessage = string.Empty;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    partial void OnErrorMessageChanged(string value)
    {
        OnPropertyChanged(nameof(HasError));
    }

    public MainPageViewModel(ApiService apiService)
    {
        _apiService = apiService;
    }

    [RelayCommand]
    private async Task LoadData()
    {
        if (IsLoading) return;

        try
        {
            IsLoading = true;
            ErrorMessage = string.Empty;

            var routeSummaries = await _apiService.GetRouteSummariesAsync();

            RouteCards.Clear();
            foreach (var item in routeSummaries)
            {
                RouteCards.Add(RouteCardItem.FromSummary(item));
            }

            if (RouteCards.Count == 0)
            {
                ErrorMessage = "Không có tour để hiển thị.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Không thể tải dữ liệu từ server: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}

public sealed class RouteCardItem
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string StopsText { get; init; } = string.Empty;
    public string MinutesText { get; init; } = string.Empty;
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
            StopsText = $"{summary.StopCount} STOPS",
            MinutesText = $"{summary.DurationMinutes} MINS",
            IconGlyph = icon,
            IconBackgroundColor = color
        };
    }
}
