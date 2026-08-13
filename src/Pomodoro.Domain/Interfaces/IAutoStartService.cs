namespace Pomodoro.Domain.Interfaces;

public interface IAutoStartService
{
    Task<bool> IsEnabledAsync(CancellationToken ct = default);
    Task EnableAsync(CancellationToken ct = default);
    Task DisableAsync(CancellationToken ct = default);
}
