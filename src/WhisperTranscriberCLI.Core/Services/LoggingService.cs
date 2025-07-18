using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace WhisperTranscriberCLI.Core.Services;

public static class LoggingService
{
    public static IServiceCollection AddLogging(this IServiceCollection services, bool isConsoleApp = false)
    {
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            
            if (isConsoleApp)
            {
                builder.AddConsole();
            }
            else
            {
                builder.AddDebug();
            }
            
            builder.SetMinimumLevel(LogLevel.Information);
            
#if DEBUG
            builder.SetMinimumLevel(LogLevel.Debug);
#endif
        });

        return services;
    }
}