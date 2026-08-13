using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Pomodoro.Infrastructure.Logging;

/// <summary>
/// Centralised Serilog configuration. Writes to both console and a rolling
/// file in the user's local app data directory.
/// </summary>
public static class LoggingConfigurator
{
    public static Logger Configure(string logsDir)
    {
        if (!Directory.Exists(logsDir))
            Directory.CreateDirectory(logsDir);

        var logPath = Path.Combine(logsDir, "pomodoro-.log");

        return new LoggerConfiguration()
            .MinimumLevel.Information()
            .Enrich.WithProperty("App", "Pomodoro")
            .Enrich.WithProperty("Version", "1.0.0")
            .WriteTo.Console(outputTemplate:
                "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext} | {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(logPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate:
                "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} | {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }
}
