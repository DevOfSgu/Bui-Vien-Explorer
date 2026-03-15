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
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (_isLoaded) return;
        _isLoaded = true;

        _viewModel.LoadDataCommand.Execute(null);
    }
}
