using System;
using Avalonia.Threading;

namespace Pomodoro.App.Services;

/// <summary>
/// Drives the per-second tick using Avalonia's DispatcherTimer.
/// Tick fires on the UI thread, which is exactly what we want
/// (engine state is consumed by ViewModels on the UI thread).
/// </summary>
internal sealed class DispatcherTimerTickScheduler : ITickScheduler, IDisposable
{
    private readonly DispatcherTimer _timer;

    public event EventHandler? Tick;

    /// <summary>The dispatcher the internal timer is bound to (used by tests).</summary>
    internal Dispatcher Dispatcher => _timer.Dispatcher;

    public DispatcherTimerTickScheduler()
    {
        /*        _timer = new DispatcherTimer(DispatcherPriority.Normal, Dispatcher.UIThread)
                {
                    Interval = TimeSpan.FromSeconds(1),
                };
                _timer.Tick += OnTick;
        */
        _timer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Normal, OnTick);
    }

    public void Start() => _timer.Start();
    public void Stop() => _timer.Stop();

    private void OnTick(object? sender, EventArgs e) => Tick?.Invoke(this, e);

    public void Dispose() => _timer.Stop();
}
