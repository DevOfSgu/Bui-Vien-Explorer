using TravelSystem.Mobile.Services;
using TravelSystem.Mobile.ViewModels;
using System.Diagnostics;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Maui.Core;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;

namespace TravelSystem.Mobile.Views;

public partial class AudioPlayerPopup : ContentView
{
    private IAudioGuideService? _audioService;
    public ApiService? AnalyticsApiService { get; set; }
    private string _text = string.Empty;
    private string _language = "vi";
    private PoiStopItem? _stop;
    private bool _isUsingMp3 = false;
    private bool _isDragging = false;
    private Task? _mp3CheckTask;
    private CancellationTokenSource? _initCts;
    private DateTime? _playbackStartedAtUtc;
    private bool _playNarrationTracked;

    public event Action<PoiStopItem>? SeeDetailsRequested;

    public AudioPlayerPopup()
    {
        InitializeComponent();
        this.PropertyChanged += OnPopupPropertyChanged;
    }

    private void OnPopupPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IsVisible) && !IsVisible)
        {
            TrackPlayNarrationIfNeeded();
            // Auto-stop when hidden
            StopAllPlayback();
        }
    }

    private void StopAllPlayback()
    {
        try 
        {
            if (AudioMediaPlayer != null)
                AudioMediaPlayer.Stop();
                
            if (_audioService != null)
                _audioService.Stop(true);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ERROR][AUDIO] Error stopping playback: {ex.Message}");
        }
    }

    public void Initialize(IAudioGuideService audioService, PoiStopItem stop, string language)
    {
        _initCts?.Cancel();
        _initCts = new CancellationTokenSource();

        // 1. Dừng ngay lập tức mọi âm thanh cũ để tránh chồng chéo (Fix lỗi Sahara đọc tiếng Cổng chào)
        StopAllPlayback();

        // 2. Unsubscribe logic cũ
        if (_audioService != null)
        {
            _audioService.PlaybackProgressChanged -= OnPlaybackProgressChanged;
            _audioService.StatusChanged -= OnAudioStatusChanged;
        }

        // 3. Reset State hoàn toàn
        _audioService = audioService;
        _stop = stop;
        _text = stop.Description ?? string.Empty;
        _language = language;
        _isUsingMp3 = false; // Mặc định là false cho đến khi check xong
        _playbackStartedAtUtc = null;
        _playNarrationTracked = false;
        
        // Clear MediaElement source ngay lập tức
        MainThread.BeginInvokeOnMainThread(() => {
            AudioMediaPlayer.Source = null;
            AudioSlider.Value = 0;
            ZoneTitleLabel.Text = stop.Name;
            if (!string.IsNullOrEmpty(stop.ImageUrl))
                ZoneImage.Source = stop.ImageUrl;
        });

        _audioService.PlaybackProgressChanged += OnPlaybackProgressChanged;
        _audioService.StatusChanged += OnAudioStatusChanged;
        
        Debug.WriteLine($"[DEBUG][TTS_POPUP] Initializing for: {stop.Name} (ZoneId: {stop.ZoneId})");

        _mp3CheckTask = CheckForMp3Async(stop, language, _initCts.Token);
        UpdatePlayPauseButton();

        // Highlight 1.0x mặc định
        MainThread.BeginInvokeOnMainThread(() => {
            var btn1x = SpeedLayout?.Children.OfType<Button>().FirstOrDefault(b => b.Text == "1.0x");
            if (btn1x != null) UpdateSpeedButtonHighlights(btn1x);
        });
    }

    private void OnAudioStatusChanged()
    {
        MainThread.BeginInvokeOnMainThread(UpdatePlayPauseButton);
    }

    private async Task CheckForMp3Async(PoiStopItem stop, string language, CancellationToken ct)
    {
        try
        {
            var db = Handler.MauiContext.Services.GetService<DatabaseService>();
            Debug.WriteLine($"[DEBUG][AUDIO] Checking database for ZoneId: {stop.ZoneId}, Language: {language}");
            var narration = await db.GetNarrationAsync(stop.ZoneId, language);
            
            if (ct.IsCancellationRequested) return;

            if (narration != null)
            {
                Debug.WriteLine($"[DEBUG][AUDIO] Narration found in DB. FileUrl: {narration.FileUrl}, LocalPath: {narration.LocalFilePath}");
                
                if (!string.IsNullOrEmpty(narration.LocalFilePath) && File.Exists(narration.LocalFilePath))
                {
                    Debug.WriteLine($"[SUCCESS][AUDIO] Local MP3 file found at: {narration.LocalFilePath}");
                    _isUsingMp3 = true;
                    _text = string.Empty;
                    await MainThread.InvokeOnMainThreadAsync(() => 
                    {
                        AudioMediaPlayer.Source = MediaSource.FromFile(narration.LocalFilePath);
                        Debug.WriteLine("[DEBUG][AUDIO] MediaElement source set to Local MP3.");
                        AudioSlider.Maximum = 1;
                    });
                }
                else
                {
                    _isUsingMp3 = false;
                    _text = narration.Text ?? stop.Description ?? string.Empty;
                    await MainThread.InvokeOnMainThreadAsync(() => 
                    {
                        AudioSlider.Maximum = 100;
                    });
                }
            }
            else
            {
                _isUsingMp3 = false;
                _text = stop.Description ?? string.Empty;
                await MainThread.InvokeOnMainThreadAsync(() => 
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
        
        Debug.WriteLine("[DEBUG][AUDIO] ShowAsync called. Waiting for MP3 check task...");
        
        // Chờ check MP3 xong mới được chạy Play (Fix lỗi tự động đọc nhầm TTS thay vì MP3)
        if (_mp3CheckTask != null)
        {
            try { await _mp3CheckTask; } catch { }
        }

        if (_isUsingMp3)
        {
            Debug.WriteLine("[DEBUG][AUDIO] Autoplaying MP3...");
            _playbackStartedAtUtc ??= DateTime.UtcNow;
            AudioMediaPlayer.Play();
        }
        else if (_audioService != null && !string.IsNullOrEmpty(_text))
        {
            Debug.WriteLine($"[DEBUG][AUDIO] Autoplaying TTS: {_text.Substring(0, Math.Min(20, _text.Length))}...");
            _playbackStartedAtUtc ??= DateTime.UtcNow;
            // Không await ở đây để tránh block việc Update UI icon ngay lúc bắt đầu
            _ = _audioService.PlayAsync(_text, _language, _audioService.CurrentSpeed);
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

    private void OnMediaStateChanged(object? sender, MediaStateChangedEventArgs e)
    {
        Debug.WriteLine($"[DEBUG][AUDIO] Media State Changed: {e.NewState}");
        MainThread.BeginInvokeOnMainThread(UpdatePlayPauseButton);
    }

    private void OnMediaEnded(object? sender, EventArgs e)
    {
        Debug.WriteLine("[DEBUG][TTS_POPUP] MP3 Playback Finished.");
        TrackPlayNarrationIfNeeded();
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
        if (sender is Button btn && double.TryParse(btn.CommandParameter?.ToString(), System.Globalization.CultureInfo.InvariantCulture, out var speed))
        {
            Debug.WriteLine($"[DEBUG][TTS_POPUP] Setting speed: {speed}x");
            
            if (_isUsingMp3)
                AudioMediaPlayer.Speed = speed;
            else if (_audioService != null)
                _audioService.CurrentSpeed = speed;

            UpdateSpeedButtonHighlights(btn);
        }
    }

    private void UpdateSpeedButtonHighlights(Button selectedBtn)
    {
        if (SpeedLayout == null) return;

        foreach (var child in SpeedLayout.Children)
        {
            if (child is Button b)
            {
                bool isSelected = (b == selectedBtn);
                b.BackgroundColor = isSelected ? Color.FromArgb("#3498DB") : Color.FromArgb("#F4F7F9");
                b.TextColor = isSelected ? Colors.White : Color.FromArgb("#34495E");
            }
        }
    }

    private async void OnReplayClicked(object sender, EventArgs e)
    {
        Debug.WriteLine("[DEBUG][AUDIO] Replay clicked.");
        _playbackStartedAtUtc ??= DateTime.UtcNow;
        if (_isUsingMp3)
        {
            AudioMediaPlayer.SeekTo(TimeSpan.Zero);
            AudioMediaPlayer.Play();
        }
        else if (_audioService != null)
        {
            await _audioService.PlayAsync(_text, _language, _audioService.CurrentSpeed);
        }
        UpdatePlayPauseButton();
    }

    private async void OnPlayPauseClicked(object sender, EventArgs e)
    {
        _playbackStartedAtUtc ??= DateTime.UtcNow;
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

        PlayPauseButton.Source = isPlaying ? "pause.png" : "play.png";
    }

    private void OnSeeDetailsClicked(object sender, EventArgs e)
    {
        if (_stop != null)
        {
            TrackPlayNarrationIfNeeded();
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
        TrackPlayNarrationIfNeeded();

        if (_isUsingMp3)
            AudioMediaPlayer.Stop();
        else if (_audioService != null)
        {
            _audioService.PlaybackProgressChanged -= OnPlaybackProgressChanged;
            _audioService.Stop();
        }
        this.IsVisible = false;
    }

    private void TrackPlayNarrationIfNeeded()
    {
        if (_playNarrationTracked || _stop == null)
        {
            return;
        }

        var apiService = AnalyticsApiService ?? Handler?.MauiContext?.Services.GetService<ApiService>();
        if (apiService == null)
        {
            return;
        }

        var dwell = 0;
        if (_playbackStartedAtUtc.HasValue)
        {
            dwell = Math.Max(0, (int)Math.Round((DateTime.UtcNow - _playbackStartedAtUtc.Value).TotalSeconds));
        }

        _playNarrationTracked = true;
        _ = apiService.TrackPlayNarrationAsync(_stop.ZoneId, dwell, _language);
    }
}
