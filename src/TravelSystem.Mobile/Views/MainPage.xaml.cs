using TravelSystem.Mobile.ViewModels;

namespace TravelSystem.Mobile.Views;

public partial class MainPage : ContentPage
{
    private readonly MainPageViewModel _viewModel;
    private bool _isLoaded;

    public MainPage(MainPageViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
        _viewModel.PropertyChanged += ViewModelOnPropertyChanged;
    }

    private void ViewModelOnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainPageViewModel.NavigatingToDetail))
            return;

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            var overlay = this.FindByName<Grid>("DetailLoadingOverlay");
            if (overlay == null)
                return;

            if (_viewModel.NavigatingToDetail)
            {
                overlay.IsVisible = true;
                await overlay.FadeTo(1, 120, Easing.CubicOut);
            }
            else
            {
                await overlay.FadeTo(0, 120, Easing.CubicIn);
                overlay.IsVisible = false;
            }
        });
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (_isLoaded) return;
        _isLoaded = true;

        _viewModel.LoadDataCommand.Execute(null);
    }
}
