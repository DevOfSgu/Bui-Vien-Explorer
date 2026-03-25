using TravelSystem.Mobile.ViewModels;
using CommunityToolkit.Maui.Views;
using TravelSystem.Mobile.Services;
using System.Diagnostics;

namespace TravelSystem.Mobile.Views;

public partial class ZoneDetailPage : ContentPage
{
    private readonly ZoneDetailViewModel _viewModel;
    
    public ZoneDetailPage(ZoneDetailViewModel viewModel)
	{
		InitializeComponent();
		_viewModel = viewModel;
		BindingContext = _viewModel;
	}


    protected override void OnAppearing()
    {
        base.OnAppearing();
        Shell.SetTabBarIsVisible(this, false);
    }
}
