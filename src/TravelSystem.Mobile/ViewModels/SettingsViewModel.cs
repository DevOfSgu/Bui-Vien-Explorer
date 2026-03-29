using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Diagnostics;
using TravelSystem.Mobile.Services;

namespace TravelSystem.Mobile.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly LocalizationManager _localizationManager;

    [ObservableProperty]
    private string _currentLanguage = "English";

    [ObservableProperty]
    private bool _isDarkMode;

    [ObservableProperty]
    private double _autoPlayRadius = 25;

    [ObservableProperty]
    private string _narratorVoice = "Female";

    private readonly DatabaseService _dbService;

    public SettingsViewModel(DatabaseService dbService)
    {
        _dbService = dbService;
        _localizationManager = LocalizationManager.Instance;
        
        // Mặc định ban đầu luôn là False cho Dark Mode
        IsDarkMode = false;

        // Load persisted settings
        _ = LoadSettingsAsync();
    }

    private async Task LoadSettingsAsync()
    {
        var isDarkStr = await _dbService.GetSettingAsync("IsDarkMode", "false");
        IsDarkMode = bool.Parse(isDarkStr);

        var langCode = await _dbService.GetSettingAsync("Language", "en");
        _localizationManager.SetLanguage(langCode);
        CurrentLanguage = _localizationManager.GetLanguageDisplayName(langCode);
    }

    // Được gọi từ code-behind của SettingsPage
    public async void SetLanguage(string langCode)
    {
        CurrentLanguage = _localizationManager.GetLanguageDisplayName(langCode);
        await _dbService.SetSettingAsync("Language", langCode);
        _localizationManager.SetLanguage(langCode);
        Debug.WriteLine($"[SETTINGS] Saved Language: {langCode}");
    }

    partial void OnIsDarkModeChanged(bool value)
    {
        if (Application.Current != null)
        {
            Application.Current.UserAppTheme = value ? AppTheme.Dark : AppTheme.Light;
            _ = _dbService.SetSettingAsync("IsDarkMode", value.ToString().ToLower());
        }
    }

    [RelayCommand]
    private void ToggleVoice(string voice)
    {
        NarratorVoice = voice;
    }

    [RelayCommand]
    private async Task SignInOut()
    {
        await Shell.Current.DisplayAlert(
            _localizationManager["settings_notice_title"],
            _localizationManager["settings_signin_dev"],
            "OK");
    }

    [RelayCommand]
    private async Task OpenHelp()
    {
        await Shell.Current.DisplayAlert(
            _localizationManager["settings_help_title"],
            _localizationManager["settings_help_msg"],
            "OK");
    }

    [RelayCommand]
    private async Task OpenTerms()
    {
        await Shell.Current.DisplayAlert(
            _localizationManager["settings_terms_title"],
            _localizationManager["settings_terms_msg"],
            "OK");
    }
}
