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
        SelectLanguage("vi", "Tiếng Việt");
    }

    private void OnSelectEnglish(object sender, EventArgs e)
    {
        SelectLanguage("en", "English");
    }

    private void OnSelectJapanese(object sender, EventArgs e)
    {
        SelectLanguage("ja", "日本語");
    }

    private void SelectLanguage(string langCode, string displayName)
    {
        if (BindingContext is SettingsViewModel vm)
        {
            vm.SetLanguage(langCode, displayName);
        }
        LanguageOverlay.IsVisible = false;
    }
}
