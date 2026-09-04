using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;

namespace Pomodoro.App.ViewModels;

public abstract class BaseViewModel : ObservableObject
{
    protected ILogger? Logger { get; }

    protected BaseViewModel(ILogger? logger = null)
    {
        Logger = logger;
    }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    private string? _errorMessage;
    public string? ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    protected Task RunSafeAsync(Func<Task> action, CancellationToken ct = default,
        [CallerMemberName] string? operation = null)
        => RunSafeAsync(_ => action(), ct, operation);

    protected async Task RunSafeAsync(Func<CancellationToken, Task> action, CancellationToken ct = default,
        [CallerMemberName] string? operation = null)
    {
        try
        {
            IsBusy = true;
            ErrorMessage = null;
            await action(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Expected on shutdown/navigation — not an error.
        }
        catch (Exception ex)
        {
            Logger?.LogWarning(ex, "{Operation} failed", operation ?? GetType().Name);
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Fire-and-forget for constructor / event-handler initiation.
    /// The task is observed internally (failures are logged + surfaced via
    /// <see cref="ErrorMessage"/>), so no unobserved exceptions escape.
    /// Continuations stay on the captured (UI) context — do not wrap in Task.Run.
    /// </summary>
    protected void FireAndForget(Func<Task> action,
        [CallerMemberName] string? operation = null)
    {
        _ = InvokeAsync(action, operation);
    }

    private async Task InvokeAsync(Func<Task> action, string? operation)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            Logger?.LogWarning(ex, "{Operation} failed (fire-and-forget)", operation ?? GetType().Name);
            ErrorMessage = ex.Message;
        }
    }
}
