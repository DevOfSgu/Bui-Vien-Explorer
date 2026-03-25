using TravelSystem.Mobile.Services;
using TravelSystem.Mobile.ViewModels;
using System.Diagnostics;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Maui.Core;

namespace TravelSystem.Mobile.Views;

public partial class AudioPlayerPopup : ContentView
{
    private IAudioGuideService? _audioService;
    private string _text = string.Empty;
    private string _language = "vi";
    private PoiStopItem? _stop;
    private bool _isUsingMp3 = false;
    private bool _isDragging = false;

    public event Action<PoiStopItem>? SeeDetailsRequested;

    public AudioPlayerPopup()
    {
        InitializeComponent();
    }

    public void Initialize(IAudioGuideService audioService, PoiStopItem stop, string language)
    {
        // Unsubscribe if reusing
        if (_audioService != null)
            _audioService.PlaybackProgressChanged -= OnPlaybackProgressChanged;

        _audioService = audioService;
        _stop = stop;
        _text = stop.Description ?? string.Empty;
        _language = language;

        ZoneTitleLabel.Text = stop.Name;
        if (!string.IsNullOrEmpty(stop.ImageUrl))
            ZoneImage.Source = stop.ImageUrl;

        _audioService.PlaybackProgressChanged += OnPlaybackProgressChanged;
        
        Debug.WriteLine($"[DEBUG][TTS_POPUP] Initialized for: {stop.Name}");

        // Reset Slider
        AudioSlider.Value = 0;

        // Kiểm tra xem có file MP3 local chưa
        _ = CheckForMp3Async(stop, language);

        UpdatePlayPauseButton();
    }

    private async Task CheckForMp3Async(PoiStopItem stop, string language)
    {
        try
        {
            var db = Handler.MauiContext.Services.GetService<DatabaseService>();
            Debug.WriteLine($"[DEBUG][AUDIO] Checking database for ZoneId: {stop.ZoneId}, Language: {language}");
            var narration = await db.GetNarrationAsync(stop.ZoneId, language);
            
            if (narration != null)
            {
                Debug.WriteLine($"[DEBUG][AUDIO] Narration found in DB. FileUrl: {narration.FileUrl}, LocalPath: {narration.LocalFilePath}");
                
                if (!string.IsNullOrEmpty(narration.LocalFilePath) && File.Exists(narration.LocalFilePath))
                {
                    Debug.WriteLine($"[SUCCESS][AUDIO] Local MP3 file found at: {narration.LocalFilePath}");
                    _isUsingMp3 = true;
                    _text = string.Empty;
                    MainThread.BeginInvokeOnMainThread(() => 
                    {
                        AudioMediaPlayer.Source = MediaSource.FromFile(narration.LocalFilePath);
                        Debug.WriteLine("[DEBUG][AUDIO] MediaElement source set to Local MP3.");
                        AudioSlider.Maximum = 1;
                    });
                }
                else
                {
                    Debug.WriteLine($"[WARNING][AUDIO] LocalFilePath is '{narration.LocalFilePath}', but file exists on disk: {File.Exists(narration.LocalFilePath)}");
                    _isUsingMp3 = false;
                    _text = narration.Text ?? stop.Description ?? string.Empty;
                    MainThread.BeginInvokeOnMainThread(() => 
                    {
                        AudioSlider.Maximum = 100;
                    });
                }
            }
            else
            {
                Debug.WriteLine("[WARNING][AUDIO] No narration record found in DB for this Zone/Language.");
                _isUsingMp3 = false;
                _text = stop.Description ?? string.Empty;
                MainThread.BeginInvokeOnMainThread(() => 
                {
                    AudioSlider.Maximum = 100; 
                });
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ERROR][AUDIO] Error checking for MP3: {ex.Message}");
            _isUsingMp3 = false;
        }
    }

    public async Task ShowAsync()
    {
        this.IsVisible = true;
        
        if (_isUsingMp3)
        {
            AudioMediaPlayer.Play();
        }
        else if (_audioService != null)
        {
            await _audioService.PlayAsync(_text, _language, _audioService.CurrentSpeed);
        }
        
        UpdatePlayPauseButton();
    }

    private void OnPlaybackProgressChanged(int current, int total)
    {
        if (_isUsingMp3 || _isDragging) return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (total > 0)
            {
                AudioSlider.Maximum = total;
                AudioSlider.Value = current;
            }
            
            if (current >= total && total > 0)
            {
                UpdatePlayPauseButton();
            }
        });
    }

    private void OnMediaPositionChanged(object? sender, MediaPositionChangedEventArgs e)
    {
        if (_isDragging) return;

        MainThread.BeginInvokeOnMainThread(() => 
        {
            var duration = AudioMediaPlayer.Duration.TotalSeconds;
            if (duration > 0)
            {
                AudioSlider.Maximum = duration;
                AudioSlider.Value = e.Position.TotalSeconds;
            }
        });
    }

    private void OnMediaEnded(object? sender, EventArgs e)
    {
        Debug.WriteLine("[DEBUG][TTS_POPUP] MP3 Playback Finished.");
        MainThread.BeginInvokeOnMainThread(() => 
        {
            AudioSlider.Value = AudioSlider.Maximum;
            UpdatePlayPauseButton();
        });
    }

    private void OnSliderDragStarted(object sender, EventArgs e) => _isDragging = true;

    private void OnSliderDragCompleted(object sender, EventArgs e)
    {
        _isDragging = false;
        if (_isUsingMp3)
        {
            AudioMediaPlayer.SeekTo(TimeSpan.FromSeconds(AudioSlider.Value));
        }
        else if (_audioService != null)
        {
            _audioService.SeekTo((int)AudioSlider.Value);
        }
    }

    private void OnSliderValueChanged(object sender, ValueChangedEventArgs e)
    {
        // Handled in DragCompleted to avoid too many seeks
    }

    private void OnSpeedSelected(object sender, EventArgs e)
    {
        if (sender is Button btn && double.TryParse(btn.CommandParameter?.ToString(), out var speed))
        {
            Debug.WriteLine($"[DEBUG][TTS_POPUP] Setting speed: {speed}x");
            
            if (_isUsingMp3)
                AudioMediaPlayer.Speed = speed;
            else if (_audioService != null)
                _audioService.CurrentSpeed = speed;

            // Update UI feedback (highlighter)
            if (btn.Parent is FlexLayout container)
            {
                foreach (var child in container.Children)
                {
                    if (child is Button b)
                    {
                        b.BackgroundColor = (b == btn) ? Color.FromArgb("#3498DB") : Color.FromArgb("#F4F7F9");
                        b.TextColor = (b == btn) ? Colors.White : Color.FromArgb("#34495E");
                    }
                }
            }
        }
    }

    private async void OnPlayPauseClicked(object sender, EventArgs e)
    {
        if (_isUsingMp3)
        {
            if (AudioMediaPlayer.CurrentState == MediaElementState.Playing)
                AudioMediaPlayer.Pause();
            else
                AudioMediaPlayer.Play();
        }
        else
        {
            if (_audioService == null) return;
            if (_audioService.IsPlaying)
                _audioService.Pause();
            else
            {
                if (!_audioService.IsPlaying && _audioService.CurrentSentenceIndex >= (_audioService.TotalSentences - 1))
                     await _audioService.PlayAsync(_text, _language, _audioService.CurrentSpeed);
                else
                    _audioService.Resume();
            }
        }
        UpdatePlayPauseButton();
    }

    private void UpdatePlayPauseButton()
    {
        if (PlayPauseButton == null) return;

        bool isPlaying = false;
        if (_isUsingMp3)
            isPlaying = AudioMediaPlayer.CurrentState == MediaElementState.Playing;
        else
            isPlaying = _audioService?.IsPlaying ?? false;

        PlayPauseButton.Source = isPlaying ? "pause_icon.png" : "play_icon.png";
    }

    private void OnSeeDetailsClicked(object sender, EventArgs e)
    {
        if (_stop != null)
        {
            this.IsVisible = false;
            
            if (_isUsingMp3)
                AudioMediaPlayer.Pause();
            else
                _audioService?.Pause();

            SeeDetailsRequested?.Invoke(_stop);
        }
    }

    private void OnCloseClicked(object sender, EventArgs e)
    {
        if (_isUsingMp3)
            AudioMediaPlayer.Stop();
        else if (_audioService != null)
        {
            _audioService.PlaybackProgressChanged -= OnPlaybackProgressChanged;
            _audioService.Stop();
        }
        this.IsVisible = false;
    }
}
