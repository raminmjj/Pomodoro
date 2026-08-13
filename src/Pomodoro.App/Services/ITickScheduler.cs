using System;

namespace Pomodoro.App.Services;

/// <summary>
/// Abstraction over the per-second tick that drives the Pomodoro engine.
/// </summary>
public interface ITickScheduler
{
    event EventHandler? Tick;
    void Start();
    void Stop();
}
