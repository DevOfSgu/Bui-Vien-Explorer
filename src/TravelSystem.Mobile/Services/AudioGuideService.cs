using System.Diagnostics;

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
    private CancellationTokenSource? _ttsCts;
    private bool _isPlaying;
    private bool _isPaused;
    private double _currentSpeed = 1.0;
    private string[] _sentences = Array.Empty<string>();
    private int _currentSentenceIndex = 0;
    private string _currentLanguage = "vi";

    public event Action<int, int>? PlaybackProgressChanged;
    public event Action? StatusChanged;

    public bool IsPlaying => _isPlaying;
    public int TotalSentences => _sentences.Length;
    public int CurrentSentenceIndex => _currentSentenceIndex;

    public double CurrentSpeed 
    { 
        get => _currentSpeed; 
        set => _currentSpeed = value; 
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
        _ttsCts = new CancellationTokenSource();
        _isPlaying = true;
        _isPaused = false;
        StatusChanged?.Invoke();

    try
    {
        var locale = await GetLocaleAsync(_currentLanguage);
        
        // ✅ Cảnh báo nếu không tìm được locale đúng
        if (locale == null)
        {
            Debug.WriteLine($"[WARN][TTS_SERVICE] Không tìm thấy locale '{_currentLanguage}'. " +
                            "Hãy kiểm tra thiết bị đã cài gói TTS tiếng Việt chưa.");
        }

        // ✅ Áp dụng cả speed vào options
        var options = new SpeechOptions 
        {
            Locale = locale,
            Volume = 1.0f,          // 0.0 -> 1.0
            Pitch = (float)_currentSpeed,  // 0.1 -> 2.0
        };

        for (; _currentSentenceIndex < _sentences.Length; _currentSentenceIndex++)
        {
            if (_ttsCts.Token.IsCancellationRequested) break;
            await TextToSpeech.Default.SpeakAsync(
                _sentences[_currentSentenceIndex], options, _ttsCts.Token);
        }
    }
    catch (OperationCanceledException) { }
    finally
    {
        if (!_isPaused) _isPlaying = false;
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

