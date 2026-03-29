using TravelSystem.Mobile.ViewModels;

namespace TravelSystem.Mobile.Views;

public partial class SettingsPage : ContentPage
{
    public SettingsPage(SettingsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    private void OnLanguageTapped(object sender, EventArgs e)
    {
        LanguageOverlay.IsVisible = true;
    }

    private void OnOverlayDismiss(object sender, EventArgs e)
    {
        LanguageOverlay.IsVisible = false;
    }

    private void OnSelectVietnamese(object sender, EventArgs e)
    {
        SelectLanguage("vi");
    }

    private void OnSelectEnglish(object sender, EventArgs e)
    {
        SelectLanguage("en");
    }

    private void OnSelectJapanese(object sender, EventArgs e)
    {
        SelectLanguage("ja");
    }

    private void SelectLanguage(string langCode)
    {
        if (BindingContext is SettingsViewModel vm)
        {
            vm.SetLanguage(langCode);
        }
        LanguageOverlay.IsVisible = false;
    }
}
