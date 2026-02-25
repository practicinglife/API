using Serilog;
using Serilog.Events;

namespace CwAssetManager.Infrastructure.Logging;

/// <summary>
/// Centralised Serilog configuration with rolling file sink and structured JSON output.
/// </summary>
public static class LoggingConfiguration
{
    /// <summary>Configures and returns a Serilog logger suitable for production use.</summary>
    public static ILogger CreateLogger(
        string logDirectory,
        LogEventLevel minimumLevel = LogEventLevel.Information,
        bool writeToConsole = true)
    {
        Directory.CreateDirectory(logDirectory);
        var logFilePath = Path.Combine(logDirectory, "cwassetmanager-.log");

        var config = new LoggerConfiguration()
            .MinimumLevel.Is(minimumLevel)
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System.Net.Http", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithThreadId()
            .Enrich.With<CorrelationIdEnricher>()
            .WriteTo.File(
                path: logFilePath,
                formatter: new Serilog.Formatting.Json.JsonFormatter(),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                fileSizeLimitBytes: 100 * 1024 * 1024, // 100 MB
                rollOnFileSizeLimit: true,
                shared: false);

        if (writeToConsole)
        {
            config = config.WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}");
        }

        return config.CreateLogger();
    }
}
