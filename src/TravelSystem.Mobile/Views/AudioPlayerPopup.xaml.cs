using TravelSystem.Mobile.Services;
using System.Diagnostics;

namespace TravelSystem.Mobile.Views;

public partial class AudioPlayerPopup : ContentView
{
    private IAudioGuideService? _audioService;
    private string _text = string.Empty;
    private string _language = "vi";

    public AudioPlayerPopup()
    {
        InitializeComponent();
    }

    public void Initialize(IAudioGuideService audioService, string title, string text, string language, string? imageUrl)
    {
        _audioService = audioService;
        _text = text;
        _language = language;

        Debug.WriteLine($"[DEBUG][TTS_POPUP] Initialized with Title: {title}, Lang: {language}");
        Debug.WriteLine($"[DEBUG][TTS_POPUP] Text length: {text.Length} characters");

        ZoneTitleLabel.Text = title;
        if (!string.IsNullOrEmpty(imageUrl))
        {
            ZoneImage.Source = imageUrl;
        }

        UpdatePlayPauseButton();
    }

    public async Task ShowAsync()
    {
        this.IsVisible = true;
        if (_audioService != null)
        {
            await _audioService.PlayAsync(_text, _language);
            UpdatePlayPauseButton();
        }
    }

    private void OnPlayPauseClicked(object sender, EventArgs e)
    {
        if (_audioService == null) return;

        if (_audioService.IsPlaying)
        {
            Debug.WriteLine("[DEBUG][TTS_POPUP] Pause clicked");
            _audioService.Pause();
        }
        else
        {
            Debug.WriteLine("[DEBUG][TTS_POPUP] Play/Resume clicked");
            _audioService.Resume();
        }
        UpdatePlayPauseButton();
    }

    private void UpdatePlayPauseButton()
    {
        PlayPauseButton.Text = (_audioService?.IsPlaying ?? false) ? "⏸" : "▶";
    }

    private void OnRewindClicked(object sender, EventArgs e)
    {
        if (_audioService != null)
        {
            // Lùi lại 1 câu
            _audioService.SeekRelative(-1);
        }
    }

    private void OnForwardClicked(object sender, EventArgs e)
    {
        if (_audioService != null)
        {
            // Tiến tới 1 câu
            _audioService.SeekRelative(1);
        }
    }



    private void OnSpeedClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && _audioService != null)
        {
            var speedText = btn.Text.Replace("x", "");
            if (double.TryParse(speedText, out var speed))
            {
                _audioService.CurrentSpeed = speed;
            }
        }
    }

    private void OnCloseClicked(object sender, EventArgs e)
    {
        _audioService?.Stop();
        this.IsVisible = false;
    }
}
