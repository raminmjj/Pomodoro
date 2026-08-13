using Pomodoro.Domain.Entities;

namespace Pomodoro.Domain.Interfaces;

public interface IReportingService
{
    /// <summary>Get (or generate on the fly) the daily report for the given local date.</summary>
    Task<DailyReport> GetDailyReportAsync(DateTime localDate, CancellationToken ct = default);

    /// <summary>Force regenerate the daily report for the given local date.</summary>
    Task<DailyReport> RegenerateDailyReportAsync(DateTime localDate, CancellationToken ct = default);
}
