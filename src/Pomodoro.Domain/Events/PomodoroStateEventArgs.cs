using Pomodoro.Domain.Entities;
using Pomodoro.Domain.Enums;

namespace Pomodoro.Domain.Events;

public sealed class PomodoroStateEventArgs : System.EventArgs
{
    public SessionPhase Phase { get; }
    public PomodoroSession? Session { get; }
    public int CycleIndex { get; }

    public PomodoroStateEventArgs(SessionPhase phase, PomodoroSession? session, int cycleIndex)
    {
        Phase = phase;
        Session = session;
        CycleIndex = cycleIndex;
    }
}
