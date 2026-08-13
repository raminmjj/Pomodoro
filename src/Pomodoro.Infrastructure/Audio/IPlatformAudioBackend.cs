namespace Pomodoro.Infrastructure.Audio;

/// <summary>
/// Abstraction over each platform's native audio API.
/// Implementations are platform-specific and selected at runtime.
/// </summary>
public interface IPlatformAudioBackend : IDisposable
{
    string Name { get; }
    Task PlayAsync(string wavPath, float volume, CancellationToken ct = default);
    Task StopAsync(CancellationToken ct = default);
}

internal static class PlatformAudioBackendFactory
{
    public static IPlatformAudioBackend Create()
    {
        if (OperatingSystem.IsWindows()) return new WindowsAudioBackend();
        if (OperatingSystem.IsMacOS())   return new MacOsAudioBackend();
        if (OperatingSystem.IsLinux())   return new LinuxAudioBackend();

        // Fallback: do nothing
        return new NullAudioBackend();
    }
}

internal sealed class NullAudioBackend : IPlatformAudioBackend
{
    public string Name => "null";
    public Task PlayAsync(string wavPath, float volume, CancellationToken ct = default) => Task.CompletedTask;
    public Task StopAsync(CancellationToken ct = default) => Task.CompletedTask;
    public void Dispose() { }
}
