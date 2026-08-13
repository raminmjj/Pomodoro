namespace Pomodoro.Domain.Interfaces;

/// <summary>
/// Cross-platform alarm sound player — no external dependencies.
/// Implementations use native APIs:
///   - Windows: winmm.dll PlaySoundW
///   - macOS:   AVAudioPlayer via Objective-C runtime
///   - Linux:   paplay / aplay (subprocess)
/// </summary>
public interface ISoundPlayer : IAsyncDisposable
{
    /// <summary>Play a bundled sound from Assets/Sounds/{name}.wav</summary>
    Task PlayAsync(string soundName, float volume = 1.0f, CancellationToken ct = default);

    /// <summary>Stop any currently playing sound.</summary>
    Task StopAsync(CancellationToken ct = default);
}
