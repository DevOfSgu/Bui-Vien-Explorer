using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Graphics;
using System.Diagnostics;
using System.Collections.ObjectModel;
using System.ComponentModel;
using TravelSystem.Mobile.Services;
using TravelSystem.Mobile.Views;

namespace TravelSystem.Mobile.ViewModels;

public partial class MainPageViewModel : ObservableObject
{
    private readonly ApiService _apiService;
    private readonly DatabaseService _databaseService;
    private readonly LocalizationManager _localizationManager;
    private readonly SemaphoreSlim _navigationLock = new(1, 1);

    public ObservableCollection<ZoneCardItem> ZoneCards { get; } = [];

    [ObservableProperty] private bool _isLoading;
    private bool _navigatingToDetail;
    [ObservableProperty] private string _errorMessage = string.Empty;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public string ZonesAvailableText => _localizationManager.Format("main_tours_available", ZoneCards.Count);
    public string MainDiscoverText => _localizationManager["main_discover"];
    public string MainOpeningTourText => _localizationManager["main_opening_tour"];

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
        _localizationManager = LocalizationManager.Instance;
        _localizationManager.PropertyChanged += OnLocalizationChanged;
    }

    private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != "Item[]" && e.PropertyName != "Item" && e.PropertyName != string.Empty) return;

        OnPropertyChanged(nameof(ZonesAvailableText));
        OnPropertyChanged(nameof(MainDiscoverText));
        OnPropertyChanged(nameof(MainOpeningTourText));

        foreach (var card in ZoneCards)
        {
            card.UpdateLocalizedText(_localizationManager);
        }
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
                        StopsCount = tour.StopsCount,
                        DurationMinutes = tour.Duration,
                        IsFavorite = false
                    });

                    ZoneCards[^1].UpdateLocalizedText(_localizationManager);
                }
            }
            else
            {
                ErrorMessage = _localizationManager["main_no_tours_found"];
            }

            OnPropertyChanged(nameof(ZonesAvailableText));
        }
        catch (Exception ex)
        {
            ErrorMessage = $"{_localizationManager["main_load_failed"]}: {ex.Message}";
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
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public int Radius { get; init; }
    public string Address { get; init; } = "--";
    public string Hours { get; init; } = "--";
    public string IconGlyph { get; init; } = "📍";
    public Color IconBackgroundColor { get; init; } = Color.FromArgb("#FFE9E9");
    public bool CanToggleFavorite { get; init; } = true;
    public int StopsCount { get; init; }
    public int DurationMinutes { get; init; }

    [ObservableProperty] private string _stopsText = string.Empty;
    [ObservableProperty] private string _minutesText = string.Empty;
    
    [ObservableProperty] private bool _isFavorite;

    public void UpdateLocalizedText(LocalizationManager localizationManager)
    {
        StopsText = localizationManager.Format("main_stops_count", StopsCount);
        MinutesText = localizationManager.Format("main_minutes", DurationMinutes);
    }
}


