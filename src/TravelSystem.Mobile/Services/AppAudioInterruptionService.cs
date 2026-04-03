namespace TravelSystem.Mobile.Services;

public interface IAppAudioInterruptionService
{
    bool IsInterrupted { get; }
    event Action<bool>? InterruptionChanged;
    void BeginInterruption();
    void EndInterruption();
}

public class AppAudioInterruptionService : IAppAudioInterruptionService
{
    private bool _isInterrupted;

    public bool IsInterrupted => _isInterrupted;
    public event Action<bool>? InterruptionChanged;

    public void BeginInterruption()
    {
        if (_isInterrupted) return;
        _isInterrupted = true;
        InterruptionChanged?.Invoke(true);
    }

    public void EndInterruption()
    {
        if (!_isInterrupted) return;
        _isInterrupted = false;
        InterruptionChanged?.Invoke(false);
    }
}
