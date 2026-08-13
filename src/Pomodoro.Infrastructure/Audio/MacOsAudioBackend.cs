using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Pomodoro.Infrastructure.Audio;

/// <summary>
/// macOS backend: spawn the `afplay` command-line tool.
/// afplay ships with macOS since 10.5 and supports WAV/AIFF/MP3/CAF.
/// Volume is set via the -v flag (0.0 to 1.0).
///
/// Why not P/Invoke AVAudioPlayer directly?
///  - AVAudioPlayer requires binding to AVFoundation.framework via Objective-C
///    runtime, which is non-trivial and brittle to maintain.
///  - afplay is a 100% stable Apple tool, present on every macOS install.
///  - Spawning a subprocess is AOT-safe and trivial to debug.
/// </summary>
internal sealed class MacOsAudioBackend : IPlatformAudioBackend
{
    private Process? _process;

    public string Name => "afplay";

    public Task PlayAsync(string wavPath, float volume, CancellationToken ct = default)
    {
        _process?.Kill(entireProcessTree: true);
        _process?.Dispose();

        var psi = new ProcessStartInfo
        {
            FileName = "afplay",
            Arguments = $"-v {Math.Clamp(volume, 0f, 1f):0.00} \"{wavPath}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        try
        {
            _process = Process.Start(psi);
            if (_process is null) return Task.CompletedTask;

            return Task.Run(async () =>
            {
                try
                {
                    using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    linked.Token.Register(() => _process.Kill(entireProcessTree: true));
                    await _process.WaitForExitAsync(linked.Token);
                }
                catch (OperationCanceledException) { /* expected on stop */ }
                catch (Exception) { /* swallow */ }
            }, ct);
        }
        catch (Exception)
        {
            return Task.CompletedTask;
        }
    }

    public Task StopAsync(CancellationToken ct = default)
    {
        try
        {
            if (_process is { HasExited: false })
                _process.Kill(entireProcessTree: true);
        }
        catch { /* swallow */ }
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        try
        {
            if (_process is { HasExited: false })
                _process.Kill(entireProcessTree: true);
            _process?.Dispose();
        }
        catch { /* ignore */ }
    }
}
