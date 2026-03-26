using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Diagnostics;
using TravelSystem.Mobile.Services;

namespace TravelSystem.Mobile.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
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
        CurrentLanguage = langCode switch
        {
            "vi" => "Tiếng Việt",
            "en" => "English",
            "ja" => "日本語",
            _ => "English"
        };
    }

    // Được gọi từ code-behind của SettingsPage
    public async void SetLanguage(string langCode, string displayName)
    {
        CurrentLanguage = displayName;
        await _dbService.SetSettingAsync("Language", langCode);
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
        await Shell.Current.DisplayAlert("Thông báo", "Chức năng đăng nhập đang được phát triển.", "OK");
    }

    [RelayCommand]
    private async Task OpenHelp()
    {
        await Shell.Current.DisplayAlert("Trợ giúp", "Đang mở trang trợ giúp & FAQ...", "OK");
    }

    [RelayCommand]
    private async Task OpenTerms()
    {
        await Shell.Current.DisplayAlert("Điều khoản", "Đang mở Điều khoản & Chính sách...", "OK");
    }
}
