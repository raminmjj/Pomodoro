using Microsoft.Extensions.Logging;
using Pomodoro.Domain.Interfaces;

namespace Pomodoro.Infrastructure.Audio;

/// <summary>
/// Cross-platform sound player that uses ONLY native OS APIs.
/// Zero external dependencies — no NAudio, no LibVLCSharp, nothing.
///
///   - Windows: P/Invoke winmm.dll!PlaySoundW (built-in since Windows 95)
///   - macOS:   P/Invoke AVFoundation via the Objective-C runtime
///   - Linux:   spawn `paplay` (PulseAudio) or `aplay` (ALSA) subprocess
///
/// Audio files are bundled as WAV in Assets/Sounds/{name}.wav.
/// WAV is preferred over MP3 because every native API supports it without
/// additional codec dependencies.
/// </summary>
public sealed class CrossPlatformSoundPlayer : ISoundPlayer
{
    private readonly ILogger<CrossPlatformSoundPlayer> _logger;
    private readonly string _soundsDir;
    private readonly IPlatformAudioBackend _backend;

    public CrossPlatformSoundPlayer(ILogger<CrossPlatformSoundPlayer> logger, string? soundsDir = null)
    {
        _logger = logger;
        _soundsDir = soundsDir ?? Path.Combine(AppContext.BaseDirectory, "Assets", "Sounds");
        _backend = PlatformAudioBackendFactory.Create();
        _logger.LogInformation("Sound player initialized: backend={Backend}", _backend.Name);
    }

    public async Task PlayAsync(string soundName, float volume = 1.0f, CancellationToken ct = default)
    {
        var path = Path.Combine(_soundsDir, $"{soundName}.wav");
        if (!File.Exists(path))
        {
            _logger.LogWarning("Sound file not found: {Path}", path);
            return;
        }

        try
        {
            await _backend.PlayAsync(path, volume, ct);
            _logger.LogDebug("Played sound {Name} at volume {Volume}", soundName, volume);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to play sound {Name}", soundName);
        }
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        try { await _backend.StopAsync(ct); }
        catch (Exception ex) { _logger.LogWarning(ex, "Stop failed"); }
    }

    public ValueTask DisposeAsync()
    {
        try { _backend.Dispose(); } catch { /* ignore */ }
        return ValueTask.CompletedTask;
    }
}
