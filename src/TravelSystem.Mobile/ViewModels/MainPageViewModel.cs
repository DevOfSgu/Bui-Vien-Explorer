using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Graphics;
using System.Diagnostics;
using System.Collections.ObjectModel;
using TravelSystem.Mobile.Services;
using TravelSystem.Mobile.Views;

namespace TravelSystem.Mobile.ViewModels;

public partial class MainPageViewModel : ObservableObject
{
    private readonly ApiService _apiService;
    private readonly DatabaseService _databaseService;
    private readonly SemaphoreSlim _navigationLock = new(1, 1);

    public ObservableCollection<ZoneCardItem> ZoneCards { get; } = [];

    [ObservableProperty] private bool _isLoading;
    private bool _navigatingToDetail;
    [ObservableProperty] private string _errorMessage = string.Empty;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public string ZonesAvailableText => $"{ZoneCards.Count} tour có sẵn";

    public bool NavigatingToDetail
    {
        get => _navigatingToDetail;
        set => SetProperty(ref _navigatingToDetail, value);
    }

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
        try
        {
            IsLoading = true;
            ErrorMessage = string.Empty;

            var tours = await _apiService.GetToursAsync();
            
            ZoneCards.Clear();
            if (tours != null && tours.Count > 0)
            {
                foreach (var tour in tours)
                {
                    ZoneCards.Add(new ZoneCardItem
                    {
                        Id = tour.Id,
                        Name = tour.Name,
                        Description = tour.Description ?? string.Empty,
                        ImageUrl = tour.ImageUrl ?? string.Empty,
                        StopsText = $"{tour.StopsCount} điểm dừng",
                        MinutesText = $"{tour.Duration} phút",

                        IsFavorite = false
                    });
                }
            }
            else
            {
                ErrorMessage = "Không có tour nào được tìm thấy.";
            }

            OnPropertyChanged(nameof(ZonesAvailableText));
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Không thể tải dữ liệu: {ex.Message}";
            Debug.WriteLine($"[MAIN_VM] Error: {ex}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task OpenTourDetails(ZoneCardItem item)
    {
        if (item == null) return;

        if (!await _navigationLock.WaitAsync(0))
            return;

        try
        {
            NavigatingToDetail = true;
            await Task.Delay(120);
            var route = $"{nameof(TourDetailPage)}?tourId={item.Id}&tourName={Uri.EscapeDataString(item.Name)}";
            await Shell.Current.GoToAsync(route);
        }
        finally
        {
            NavigatingToDetail = false;
            _navigationLock.Release();
        }
    }

    private Task ToggleFavorite(ZoneCardItem item, CancellationToken c)
    {
        return Task.CompletedTask;
    }

}

public partial class ZoneCardItem : ObservableObject
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string ImageUrl { get; init; } = string.Empty;
    public string IconGlyph { get; init; } = "📍";
    public Color IconBackgroundColor { get; init; } = Color.FromArgb("#FFE9E9");
    public bool CanToggleFavorite { get; init; } = true;
    public string StopsText { get; init; } = "1 điểm dừng";
    public string MinutesText { get; init; } = "8 phút";
    
    [ObservableProperty] private bool _isFavorite;
}


