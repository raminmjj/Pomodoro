using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace Pomodoro.Infrastructure.Audio;

/// <summary>
/// Windows backend: P/Invoke directly into winmm.dll!PlaySoundW.
/// Plays WAV files synchronously on a background thread.
///
/// Notes:
///  - SND_ASYNC would play on the calling thread, but we want cancellation support.
///  - SND_FILENAME: load from file path.
///  - SND_NODEFAULT: don't play the default beep if the file is missing.
///
/// No external dependencies. Works on every Windows version since Windows 95.
/// </summary>
internal sealed class WindowsAudioBackend : IPlatformAudioBackend
{
    private const string WinMm = "winmm.dll";

    private const uint SND_FILENAME = 0x00020000;
    private const uint SND_NODEFAULT = 0x00000002;
    private const uint SND_ASYNC = 0x00000001;

    [DllImport(WinMm, SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool PlaySoundW(string pszSound, IntPtr hmod, uint fdwSound);

    [DllImport(WinMm, SetLastError = true)]
    private static extern bool PlaySoundW(IntPtr pszSound, IntPtr hmod, uint fdwSound);  // pass null to stop

    private CancellationTokenSource? _cts;

    public string Name => "winmm.dll";

    public Task PlayAsync(string wavPath, float volume, CancellationToken ct = default)
    {
        _cts?.Cancel();
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        var token = _cts.Token;
        // winmm doesn't support volume scaling directly. We pass through unchanged.
        // Volume scaling would require using waveOutOpen + waveOutSetVolume — much more code.
        // For now, accept volume=1.0 and document the limitation.

        return Task.Run(() =>
        {
            try
            {
                if (token.IsCancellationRequested) return;
                // SND_ASYNC + a manual wait gives us cancellation semantics
                PlaySoundW(wavPath, IntPtr.Zero, SND_FILENAME | SND_NODEFAULT | SND_ASYNC);

                // Wait roughly the duration of the file so Stop has time to fire
                // winmm doesn't give us a duration API, so we estimate from file size:
                // WAV is ~176400 bytes/sec at 44.1kHz 16-bit stereo.
                var fileInfo = new FileInfo(wavPath);
                var approxDurationMs = fileInfo.Length / 176400.0 * 1000;
                var waitMs = (int)Math.Min(approxDurationMs, 10_000);
                token.WaitHandle.WaitOne(waitMs);
            }
            catch (OperationCanceledException) { /* expected */ }
        }, token);
    }

    public Task StopAsync(CancellationToken ct = default)
    {
        _cts?.Cancel();
        // Passing null pszSound with SND_PURGE stops any currently playing sound.
        PlaySoundW(IntPtr.Zero, IntPtr.Zero, 0);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
