using Pomodoro.Domain.Entities;

namespace Pomodoro.Domain.Interfaces;

public interface IReportingService
{
    /// <summary>Get (or generate on the fly) the daily report for the given date.</summary>
    Task<DailyReport> GetDailyReportAsync(DateTime dateUtc, CancellationToken ct = default);

    /// <summary>Force regenerate the daily report for the given date.</summary>
    Task<DailyReport> RegenerateDailyReportAsync(DateTime dateUtc, CancellationToken ct = default);
}
