using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Pomodoro.Infrastructure.Audio;

/// <summary>
/// Linux backend: try `paplay` (PulseAudio, present on most desktop distros),
/// fall back to `aplay` (ALSA, present on essentially every Linux).
///
/// Both tools ship with their respective sound servers and require no extra
/// dependencies on the user's machine beyond the standard desktop audio stack.
/// </summary>
internal sealed class LinuxAudioBackend : IPlatformAudioBackend
{
    private Process? _process;

    public string Name => "paplay/aplay";

    public Task PlayAsync(string wavPath, float volume, CancellationToken ct = default)
    {
        _process?.Kill(entireProcessTree: true);
        _process?.Dispose();

        // Try PulseAudio first (modern Linux desktops)
        var (fileName, args) = FindLinuxPlayer(wavPath);
        if (fileName is null) return Task.CompletedTask;

        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = args,
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
                    linked.Token.Register(() =>
                    {
                        try { _process.Kill(entireProcessTree: true); } catch { }
                    });
                    await _process.WaitForExitAsync(linked.Token);
                }
                catch (OperationCanceledException) { /* expected */ }
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

    private static (string? fileName, string args) FindLinuxPlayer(string wavPath)
    {
        // PulseAudio paplay supports volume via --volume=0..65536
        if (FileExistsOnPath("paplay"))
        {
            var volInt = (int)(Math.Clamp(1f, 0f, 1f) * 65536);
            return ("paplay", $"--volume={volInt} \"{wavPath}\"");
        }
        // ALSA aplay doesn't support volume scaling — it plays at native volume.
        if (FileExistsOnPath("aplay"))
        {
            return ("aplay", $"-q \"{wavPath}\"");
        }
        return (null, string.Empty);
    }

    private static bool FileExistsOnPath(string fileName)
    {
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(dir)) continue;
            try
            {
                var path = Path.Combine(dir.Trim('"'), fileName);
                if (File.Exists(path)) return true;
            }
            catch { /* ignore */ }
        }
        return false;
    }
}
