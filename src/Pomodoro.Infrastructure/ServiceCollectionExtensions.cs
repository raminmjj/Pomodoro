using Microsoft.Extensions.DependencyInjection;
using Pomodoro.Application.Services;
using Pomodoro.Domain.Entities;
using Pomodoro.Domain.Interfaces;
using Pomodoro.Infrastructure.Audio;
using Pomodoro.Infrastructure.Autostart;
using Pomodoro.Infrastructure.Hooks;
using Pomodoro.Infrastructure.Notifications;
using Pomodoro.Infrastructure.Persistence;
using Pomodoro.Infrastructure.Persistence.Mappers;

namespace Pomodoro.Infrastructure;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all infrastructure services. Call from Program.cs.
    /// </summary>
    /// <param name="dbPath">Path to the SQLite file.</param>
    /// <param name="soundsDir">Path to the bundled WAV sounds directory.</param>
    public static IServiceCollection AddPomodoroInfrastructure(
        this IServiceCollection services,
        string dbPath,
        string soundsDir)
    {
        // Mappers (AOT-safe: concrete implementations, no reflection)
        services.AddSingleton<ISqliteMapper<TaskItem>, TaskItemMapper>();
        services.AddSingleton<ISqliteMapper<PomodoroSession>, PomodoroSessionMapper>();
        services.AddSingleton<ISqliteMapper<BreakActivity>, BreakActivityMapper>();
        services.AddSingleton<ISqliteMapper<Setting>, SettingMapper>();
        services.AddSingleton<ISqliteMapper<DailyReport>, DailyReportMapper>();

        // Persistence
        services.AddSingleton<SqliteDbContext>(sp => new SqliteDbContext(
            dbPath,
            sp.GetRequiredService<ISqliteMapper<TaskItem>>(),
            sp.GetRequiredService<ISqliteMapper<PomodoroSession>>(),
            sp.GetRequiredService<ISqliteMapper<BreakActivity>>(),
            sp.GetRequiredService<ISqliteMapper<Setting>>(),
            sp.GetRequiredService<ISqliteMapper<DailyReport>>(),
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<SqliteDbContext>>()));

        // Generic repository
        services.AddSingleton(typeof(IRepository<>), typeof(SqliteRepository<>));

        // Services
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<ITaskService, TaskService>();
        services.AddSingleton<IReportingService, ReportingService>();
        services.AddSingleton<IActivityAlertEvaluator, ActivityAlertEvaluator>();

        // Engine
        services.AddSingleton<Pomodoro.Application.Engines.PomodoroEngine>();
        services.AddSingleton<IPomodoroEngine>(sp => sp.GetRequiredService<Pomodoro.Application.Engines.PomodoroEngine>());

        // Activity tracker — always register SharpHook, conditionally used
        services.AddSingleton<IActivityTracker, SharpHookActivityTracker>();

        // Audio — cross-platform pure P/Invoke implementation
        services.AddSingleton<ISoundPlayer>(sp =>
            new CrossPlatformSoundPlayer(
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<CrossPlatformSoundPlayer>>(),
                soundsDir));

        // Notifications
        services.AddSingleton<AvaloniaNotificationService>();
        services.AddSingleton<INotificationService>(sp => sp.GetRequiredService<AvaloniaNotificationService>());

        // Autostart — platform-specific
        services.AddSingleton<IAutoStartService>(sp => AutoStartServiceFactory.Create(sp));

        return services;
    }
}
