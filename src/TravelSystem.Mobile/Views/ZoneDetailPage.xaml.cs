using TravelSystem.Mobile.ViewModels;
using CommunityToolkit.Maui.Views;
using TravelSystem.Mobile.Services;
using System.Diagnostics;

namespace TravelSystem.Mobile.Views;

public partial class ZoneDetailPage : ContentPage
{
    private readonly ZoneDetailViewModel _viewModel;
    private readonly IAudioGuideService _audioService;
    private readonly DatabaseService _dbService;

    public ZoneDetailPage(ZoneDetailViewModel viewModel, IAudioGuideService audioService, DatabaseService dbService)
	{
		InitializeComponent();
		_viewModel = viewModel;
        _audioService = audioService;
        _dbService = dbService;
		BindingContext = _viewModel;
	}

    private async void OnPlayAudioClicked(object sender, EventArgs e)
    {
        try
        {
            Debug.WriteLine($"[DEBUG][TTS_FLOW] Play button clicked for Zone: {_viewModel.Name} (Id: {_viewModel.ZoneId})");
            var lang = Preferences.Get("Language", "vi"); 
            Debug.WriteLine($"[DEBUG][TTS_FLOW] Using language: {lang}");

            var narration = await _dbService.GetNarrationAsync(_viewModel.ZoneId, lang);

            if (narration != null && !string.IsNullOrEmpty(narration.Text))
            {
                Debug.WriteLine("[DEBUG][TTS_FLOW] Found narration in local database.");
                AudioPlayer.Initialize(_audioService, _viewModel.Name, narration.Text, lang, _viewModel.ImageUrl);
                _ = AudioPlayer.ShowAsync();
            }
            else
            {
                Debug.WriteLine("[DEBUG][TTS_FLOW] No narration found in DB, falling back to zone description.");
                AudioPlayer.Initialize(_audioService, _viewModel.Name, _viewModel.Description, lang, _viewModel.ImageUrl);
                _ = AudioPlayer.ShowAsync();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ZONE_PAGE] Error showing audio popup: {ex.Message}");
        }
    }


    protected override void OnAppearing()
    {
        base.OnAppearing();
        Shell.SetTabBarIsVisible(this, false);
    }
}
