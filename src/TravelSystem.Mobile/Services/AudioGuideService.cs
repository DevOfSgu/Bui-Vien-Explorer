using System.Diagnostics;
using System.Linq;

namespace TravelSystem.Mobile.Services;

public interface IAudioGuideService
{
    Task PlayAsync(string text, string language, double speed = 1.0);
    void Pause();
    void Resume();
    void Stop(bool hardStop = true);
    bool IsPlaying { get; }
    double CurrentSpeed { get; set; }
    void SeekTo(int index);
    void SeekRelative(int sentenceDelta);
    int TotalSentences { get; }
    int CurrentSentenceIndex { get; }
    event Action<int, int>? PlaybackProgressChanged;
    event Action? StatusChanged;
}


public class AudioGuideService : IAudioGuideService
{
    private readonly IAppAudioInterruptionService _audioInterruptionService;
    private CancellationTokenSource? _ttsCts;
    private bool _isPlaying;
    private bool _isPaused;
    private bool _resumeAfterInterruption;
    private double _currentSpeed = 1.0;
    private string[] _sentences = Array.Empty<string>();
    private int _currentSentenceIndex = 0;
    private string _currentLanguage = "vi";

    public event Action<int, int>? PlaybackProgressChanged;
    public event Action? StatusChanged;

    public bool IsPlaying => _isPlaying;
    public int TotalSentences => _sentences.Length;
    public int CurrentSentenceIndex => _currentSentenceIndex;

    public AudioGuideService(IAppAudioInterruptionService audioInterruptionService)
    {
        _audioInterruptionService = audioInterruptionService;
        _audioInterruptionService.InterruptionChanged += OnInterruptionChanged;
    }

    public double CurrentSpeed 
    { 
        get => _currentSpeed; 
        set 
        {
            if (Math.Abs(_currentSpeed - value) > 0.01)
            {
                _currentSpeed = value;
                if (_isPlaying && !_isPaused)
                {
                    // Restart playback from current sentence with new speed
                    _ = StartPlaybackAsync();
                }
            }
        }
    }

    public async Task PlayAsync(string text, string language, double speed = 1.0)
    {
        _currentLanguage = language;
        _currentSpeed = speed;

        _sentences = text.Split(new[] { '.', '?', '!', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                       .Select(s => s.Trim())
                       .Where(s => !string.IsNullOrEmpty(s))
                       .ToArray();
        
        _currentSentenceIndex = 0;
        _isPaused = false;
        
        Debug.WriteLine($"[DEBUG][TTS_SERVICE] PlayAsync started. Total sentences: {_sentences.Length}");
        await StartPlaybackAsync();
    }

    private async Task StartPlaybackAsync()
    {
        Stop(hardStop: false);
        var cts = new CancellationTokenSource();
        _ttsCts = cts;
        _isPlaying = true;
        _isPaused = false;
        StatusChanged?.Invoke();

    try
    {
        var locale = await GetLocaleAsync(_currentLanguage);
        
        if (locale == null)
            Debug.WriteLine($"[WARN][TTS_SERVICE] Không tìm thấy locale '{_currentLanguage}'");

        var options = new SpeechOptions 
        {
            Locale = locale,
            Volume = 1.0f,
            Rate = (float)_currentSpeed,
            Pitch = 1.0f 
        };

        for (; _currentSentenceIndex < _sentences.Length; _currentSentenceIndex++)
        {
            if (cts.Token.IsCancellationRequested) break;
            
            await TextToSpeech.Default.SpeakAsync(
                _sentences[_currentSentenceIndex], options, cts.Token);
                
            PlaybackProgressChanged?.Invoke(_currentSentenceIndex + 1, _sentences.Length);
        }
    }
    catch (OperationCanceledException) { }
    catch (Exception ex)
    {
        Debug.WriteLine($"[ERROR][TTS_SERVICE] {ex.Message}");
    }
    finally
    {
        // Chỉ reset _isPlaying khi cts này thực sự hoàn thành (tức là không bị hủy bởi cts khác)
        if (_ttsCts == cts)
        {
            if (!_isPaused)
            {
                _isPlaying = false;
                StatusChanged?.Invoke();
            }
        }
    }
    }

    public void Pause()
    {
        if (!_isPlaying) return;
        _isPaused = true;
        _isPlaying = false;
        _ttsCts?.Cancel();
        StatusChanged?.Invoke();
    }

    public void Resume()
    {
        if (!_isPaused) return;
        _ = StartPlaybackAsync();
        StatusChanged?.Invoke();
    }

    public void SeekTo(int index)
    {
        _currentSentenceIndex = Math.Clamp(index, 0, Math.Max(0, _sentences.Length - 1));
        Debug.WriteLine($"[DEBUG][TTS_SERVICE] SeekTo: {_currentSentenceIndex}");
        if (_isPlaying)
        {
            _ = StartPlaybackAsync();
        }
        else
        {
            PlaybackProgressChanged?.Invoke(_currentSentenceIndex, _sentences.Length);
        }
    }

    public void SeekRelative(int sentenceDelta)
    {
        SeekTo(_currentSentenceIndex + sentenceDelta);
    }

    public void Stop(bool hardStop = true)
    {
        Debug.WriteLine($"[DEBUG][TTS_SERVICE] Stop requested. Hard: {hardStop}");
        _ttsCts?.Cancel();
        _ttsCts?.Dispose();
        _ttsCts = null;
        
        if (hardStop)
        {
            _isPlaying = false;
            _currentSentenceIndex = 0;
        }
        StatusChanged?.Invoke();
    }

    private void OnInterruptionChanged(bool isInterrupted)
    {
        if (isInterrupted)
        {
            _resumeAfterInterruption = _isPlaying;
            if (_isPlaying)
            {
                Pause();
            }

            return;
        }

        if (_resumeAfterInterruption && _isPaused)
        {
            Resume();
        }

        _resumeAfterInterruption = false;
    }

    private async Task<Locale?> GetLocaleAsync(string languageCode)
    {
    try
    {
        var locales = await TextToSpeech.Default.GetLocalesAsync();
            // Log TẤT CẢ locale có trên máy
            Debug.WriteLine("=== DANH SÁCH LOCALE TRÊN MÁY ===");
            foreach (var l in locales)
                Debug.WriteLine($"  Name={l.Name}, Language={l.Language}, Country={l.Country}");
            Debug.WriteLine("==================================");

            // ✅ Dùng languageCode thực sự được truyền vào
            var locale = locales.FirstOrDefault(l =>
                        l.Language.Equals(languageCode, StringComparison.OrdinalIgnoreCase))
                  ?? locales.FirstOrDefault(l =>
                        l.Language.StartsWith(languageCode.Split('-')[0], 
                                              StringComparison.OrdinalIgnoreCase));

        if (locale == null)
            Debug.WriteLine($"[WARN] Không có locale nào khớp với '{languageCode}'. " +
                            $"Danh sách: {string.Join(", ", locales.Select(l => l.Language))}");
        else
            Debug.WriteLine($"[DEBUG] Dùng locale: {locale.Name} ({locale.Language})");

        return locale;
    }
    catch (Exception ex)
    {
        Debug.WriteLine($"[ERROR][TTS_SERVICE] {ex.Message}");
        return null;
    }
    }
}

