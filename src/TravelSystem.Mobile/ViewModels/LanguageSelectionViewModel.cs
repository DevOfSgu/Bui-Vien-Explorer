using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TravelSystem.Mobile.Services;


namespace TravelSystem.Mobile.ViewModels;

public partial class LanguageSelectionViewModel : ObservableObject
{
    private readonly DatabaseService _dbService;
    private readonly LocalizationManager _localizationManager;
    private string _selectedLanguage = "vi";

    public IRelayCommand<string> SelectLanguageCommand { get; }
    public IAsyncRelayCommand GetStartedCommand { get; }

    public string SelectedLanguage
    {
        get => _selectedLanguage;
        set => SetProperty(ref _selectedLanguage, value);
    }

    public LanguageSelectionViewModel(DatabaseService dbService)
    {
        _dbService = dbService;
        _localizationManager = LocalizationManager.Instance;
        SelectLanguageCommand = new RelayCommand<string>(SelectLanguage);
        GetStartedCommand = new AsyncRelayCommand(GetStarted);
    }

    private void SelectLanguage(string? langCode)
    {
        if (string.IsNullOrWhiteSpace(langCode))
            return;

        SelectedLanguage = langCode;
        _localizationManager.SetLanguage(langCode);
        Debug.WriteLine($"Selected language: {SelectedLanguage}");
    }

    private async Task GetStarted()
    {
        await _dbService.SetSettingAsync("Language", SelectedLanguage);
        _localizationManager.SetLanguage(SelectedLanguage);

        await Microsoft.Maui.ApplicationModel.MainThread.InvokeOnMainThreadAsync(async () =>
        {
            await Shell.Current.GoToAsync("//MainPage");
        });
    }
}
