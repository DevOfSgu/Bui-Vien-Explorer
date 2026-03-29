using System.ComponentModel;
using System.Globalization;
using System.Resources;
using System.Reflection;
using Microsoft.Maui.ApplicationModel;

namespace TravelSystem.Mobile.Services;

public sealed class LocalizationManager : INotifyPropertyChanged
{
    private readonly ResourceManager _resourceManager =
        new("TravelSystem.Mobile.Resources.Localization.AppResources", Assembly.GetExecutingAssembly());

    private CultureInfo _currentCulture = CultureInfo.GetCultureInfo("vi");

    public static LocalizationManager Instance { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    public string CurrentLanguage => _currentCulture.TwoLetterISOLanguageName;

    public string this[string key] => Get(key);

    public void SetLanguage(string? langCode)
    {
        var normalized = NormalizeLanguage(langCode);
        if (string.Equals(_currentCulture.TwoLetterISOLanguageName, normalized, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // Keep localization notifications on UI thread so XAML bindings do not crash
        // when language is changed from an async/background continuation.
        void ApplyLanguage()
        {
            try
            {
                _currentCulture = CultureInfo.GetCultureInfo(normalized);
            }
            catch (CultureNotFoundException)
            {
                _currentCulture = CultureInfo.GetCultureInfo("en");
            }

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentLanguage)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item"));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
        }

        if (MainThread.IsMainThread)
        {
            ApplyLanguage();
            return;
        }

        MainThread.BeginInvokeOnMainThread(ApplyLanguage);
    }

    public string Get(string key)
    {
        return _resourceManager.GetString(key, _currentCulture)
               ?? _resourceManager.GetString(key, CultureInfo.InvariantCulture)
               ?? key;
    }

    public string Format(string key, params object[] args)
    {
        var template = Get(key);
        try
        {
            return string.Format(_currentCulture, template, args);
        }
        catch (FormatException)
        {
            // Prevent app crash if one localized string has invalid placeholders.
            return template;
        }
    }

    public string GetLanguageDisplayName(string? langCode)
    {
        return NormalizeLanguage(langCode) switch
        {
            "vi" => "Tiếng Việt",
            "en" => "English",
            "ja" => "日本語",
            _ => "English"
        };
    }

    private static string NormalizeLanguage(string? langCode)
    {
        return (langCode ?? "vi").ToLowerInvariant() switch
        {
            "vi" => "vi",
            "en" => "en",
            "ja" => "ja",
            _ => "en"
        };
    }
}
