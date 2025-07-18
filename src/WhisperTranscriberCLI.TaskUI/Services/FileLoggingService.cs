using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog.Extensions.Logging.File;

namespace WhisperTranscriberCLI.TaskUI.Services;

public static class FileLoggingService
{
    public static IServiceCollection AddFileLogging(this IServiceCollection services)
    {
        services.AddSingleton<ILoggerFactory>(provider =>
        {
            var loggerFactory = new LoggerFactory();
            
            var logPath = Path.Combine(AppContext.BaseDirectory, "WhisperTranscriberUI.log");
            
            // Clear/overwrite log file on start
            if (File.Exists(logPath))
            {
                File.Delete(logPath);
            }
            
            // AddFile expects: path, minimumLevel, fileSizeLimitBytes, retainedFileCountLimit
            loggerFactory.AddFile(logPath, LogLevel.Information);
            
            return loggerFactory;
        });

        services.AddLogging(builder =>
        {
            builder.AddDebug();
            builder.SetMinimumLevel(LogLevel.Information);
            
#if DEBUG
            builder.SetMinimumLevel(LogLevel.Debug);
#endif
        });

        return services;
    }
}