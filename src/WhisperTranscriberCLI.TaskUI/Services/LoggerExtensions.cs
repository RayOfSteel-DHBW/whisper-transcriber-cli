using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.IO;

namespace WhisperTranscriberCLI.TaskUI.Services
{
    public static class LoggerExtensions
    {
        public static IServiceCollection AddFileLogging(this IServiceCollection services)
        {
            services.AddLogging(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Debug);
                
                // Add console logging for development
                builder.AddConsole();
                
                // Add debug logging for Visual Studio output
                builder.AddDebug();
                
                // Add custom file logging using Microsoft.Extensions.Logging
                var logDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "WhisperTranscriberCLI",
                    "Logs");
                
                Directory.CreateDirectory(logDirectory);
                
                var logFilePath = Path.Combine(logDirectory, $"WhisperTranscriberUI_{DateTime.Now:yyyyMMdd}.log");
                
                // Use Microsoft.Extensions.Logging file provider
                builder.AddProvider(new FileLoggerProvider(logFilePath));
            });
            
            return services;
        }
    }
    
    public class FileLoggerProvider : ILoggerProvider
    {
        private readonly string _filePath;
        private readonly object _lock = new object();

        public FileLoggerProvider(string filePath)
        {
            _filePath = filePath;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new FileLogger(categoryName, _filePath, _lock);
        }

        public void Dispose()
        {
            // No resources to dispose
        }
    }

    public class FileLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly string _filePath;
        private readonly object _lock;

        public FileLogger(string categoryName, string filePath, object lockObject)
        {
            _categoryName = categoryName;
            _filePath = filePath;
            _lock = lockObject;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel >= LogLevel.Debug;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            var message = formatter(state, exception);
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var logEntry = $"[{timestamp}] [{logLevel}] [{_categoryName}] {message}";
            
            if (exception != null)
            {
                logEntry += $"{Environment.NewLine}Exception: {exception}";
            }
            
            logEntry += Environment.NewLine;

            lock (_lock)
            {
                try
                {
                    File.AppendAllText(_filePath, logEntry);
                }
                catch
                {
                    // Ignore file write errors to prevent recursive logging issues
                }
            }
        }
    }
}