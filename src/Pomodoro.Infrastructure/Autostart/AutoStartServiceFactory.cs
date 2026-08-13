using Microsoft.Extensions.Logging;
using Pomodoro.Domain.Interfaces;

namespace Pomodoro.Infrastructure.Autostart;

/// <summary>
/// Factory that picks the right IAutoStartService for the current OS.
/// </summary>
public static class AutoStartServiceFactory
{
    public static IAutoStartService Create(IServiceProvider sp)
    {
        var loggerFactory = (ILoggerFactory)sp.GetService(typeof(ILoggerFactory))!;

        if (OperatingSystem.IsWindows())
            return new WindowsAutoStartService(loggerFactory.CreateLogger<WindowsAutoStartService>());
        if (OperatingSystem.IsMacOS())
            return new MacAutoStartService(loggerFactory.CreateLogger<MacAutoStartService>());
        if (OperatingSystem.IsLinux())
            return new LinuxAutoStartService(loggerFactory.CreateLogger<LinuxAutoStartService>());

        return new NullAutoStartService(loggerFactory.CreateLogger<NullAutoStartService>());
    }
}

internal sealed class NullAutoStartService : IAutoStartService
{
    private readonly ILogger<NullAutoStartService> _logger;
    public NullAutoStartService(ILogger<NullAutoStartService> logger) => _logger = logger;
    public Task<bool> IsEnabledAsync(CancellationToken ct = default) => Task.FromResult(false);
    public Task EnableAsync(CancellationToken ct = default) { _logger.LogWarning("Autostart not supported on this OS"); return Task.CompletedTask; }
    public Task DisableAsync(CancellationToken ct = default) => Task.CompletedTask;
}
