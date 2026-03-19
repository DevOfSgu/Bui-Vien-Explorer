using TravelSystem.Mobile.ViewModels;

namespace TravelSystem.Mobile.Views;

public partial class SavedPage : ContentPage
{
    private readonly SavedPageViewModel _viewModel;

    public SavedPage(SavedPageViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadFavoritesCommand.Execute(null);
    }
}
