using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace CosmosLeaseSample;

/// <summary>
/// Custom console logger provider that applies ANSI color codes for enhanced readability.
/// </summary>
public class ColoredConsoleLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentDictionary<string, ColoredConsoleLogger> _loggers = new();

    public ILogger CreateLogger(string categoryName)
    {
        return _loggers.GetOrAdd(categoryName, name => new ColoredConsoleLogger(name));
    }

    public void Dispose()
    {
        _loggers.Clear();
    }
}

/// <summary>
/// Console logger with color-coded output based on log level and content.
/// </summary>
public class ColoredConsoleLogger : ILogger
{
    private readonly string _categoryName;
    private static readonly object _lock = new();

    public ColoredConsoleLogger(string categoryName)
    {
        _categoryName = categoryName;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        var message = formatter(state, exception);
        if (string.IsNullOrEmpty(message))
            return;

        lock (_lock)
        {
            // Determine color based on log level and message content
            var color = GetColorForLogLevel(logLevel, message);
            
            // Apply color
            Console.ForegroundColor = color;
            
            // Format and write message
            if (message.Contains("✓"))
            {
                // Success marker - use green
                var parts = message.Split('✓');
                foreach (var part in parts)
                {
                    if (part == parts[0])
                    {
                        Console.Write(part);
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.Write("✓");
                        Console.ForegroundColor = color;
                        Console.Write(part);
                    }
                }
                Console.WriteLine();
            }
            else if (message.Contains("✗"))
            {
                // Failure marker - use red
                var parts = message.Split('✗');
                foreach (var part in parts)
                {
                    if (part == parts[0])
                    {
                        Console.Write(part);
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.Write("✗");
                        Console.ForegroundColor = color;
                        Console.Write(part);
                    }
                }
                Console.WriteLine();
            }
            else
            {
                Console.WriteLine(message);
            }

            // Write exception if present
            if (exception != null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(exception.ToString());
            }

            Console.ResetColor();
        }
    }

    private static ConsoleColor GetColorForLogLevel(LogLevel logLevel, string message)
    {
        // Special cases based on message content
        if (message.StartsWith("✓") || message.Contains("Lock acquired") || message.Contains("Completed successfully"))
            return ConsoleColor.Green;
        
        if (message.StartsWith("✗") || message.Contains("Lock unavailable") || message.Contains("failed"))
            return ConsoleColor.Red;

        // Default colors by log level
        return logLevel switch
        {
            LogLevel.Trace => ConsoleColor.DarkGray,
            LogLevel.Debug => ConsoleColor.Gray,
            LogLevel.Information => ConsoleColor.Cyan,
            LogLevel.Warning => ConsoleColor.Yellow,
            LogLevel.Error => ConsoleColor.Red,
            LogLevel.Critical => ConsoleColor.Magenta,
            _ => ConsoleColor.White
        };
    }
}

/// <summary>
/// Extension methods for colored console logging configuration.
/// </summary>
public static class ColoredConsoleLoggerExtensions
{
    /// <summary>
    /// Adds the colored console logger to the logging builder.
    /// </summary>
    public static ILoggingBuilder AddColoredConsole(this ILoggingBuilder builder)
    {
        builder.AddProvider(new ColoredConsoleLoggerProvider());
        return builder;
    }
}
