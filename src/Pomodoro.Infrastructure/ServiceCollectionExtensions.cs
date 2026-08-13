using LiteDB;
using Microsoft.Extensions.DependencyInjection;
using Pomodoro.Application.Services;
using Pomodoro.Domain.Entities;
using Pomodoro.Domain.Interfaces;
using Pomodoro.Infrastructure.Audio;
using Pomodoro.Infrastructure.Autostart;
using Pomodoro.Infrastructure.Hooks;
using Pomodoro.Infrastructure.Notifications;
using Pomodoro.Infrastructure.Persistence;

namespace Pomodoro.Infrastructure;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all infrastructure services. Call from Program.cs.
    /// </summary>
    /// <param name="dbPath">Path to the LiteDB file.</param>
    /// <param name="soundsDir">Path to the bundled WAV sounds directory.</param>
    public static IServiceCollection AddPomodoroInfrastructure(
        this IServiceCollection services,
        string dbPath,
        string soundsDir)
    {
        // Persistence
        services.AddSingleton<LiteDbContext>(_ => new LiteDbContext(
            dbPath,
            _.GetRequiredService<Microsoft.Extensions.Logging.ILogger<LiteDbContext>>()));
        services.AddSingleton<ILiteCollection<TaskItem>>(sp => sp.GetRequiredService<LiteDbContext>().Tasks);
        services.AddSingleton<ILiteCollection<PomodoroSession>>(sp => sp.GetRequiredService<LiteDbContext>().Sessions);
        services.AddSingleton<ILiteCollection<BreakActivity>>(sp => sp.GetRequiredService<LiteDbContext>().Activities);
        services.AddSingleton<ILiteCollection<Setting>>(sp => sp.GetRequiredService<LiteDbContext>().Settings);
        services.AddSingleton<ILiteCollection<DailyReport>>(sp => sp.GetRequiredService<LiteDbContext>().Reports);

        services.AddSingleton(typeof(IRepository<>), typeof(LiteRepository<>));

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
