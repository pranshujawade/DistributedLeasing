using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace DistributedLeasing.Tests.Shared.Fixtures;

/// <summary>
/// Shared logging fixture for capturing and asserting log messages in tests.
/// </summary>
public class LoggingFixture : IDisposable
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly TestLoggerProvider _loggerProvider;
    private bool _disposed;

    /// <summary>
    /// Gets the captured log entries.
    /// </summary>
    public IReadOnlyList<LogEntry> LogEntries => _loggerProvider.LogEntries;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoggingFixture"/> class with Debug minimum level.
    /// </summary>
    public LoggingFixture() : this(LogLevel.Debug)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LoggingFixture"/> class.
    /// </summary>
    /// <param name="minLevel">Minimum log level to capture.</param>
    private LoggingFixture(LogLevel minLevel)
    {
        _loggerProvider = new TestLoggerProvider(minLevel);
        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddProvider(_loggerProvider);
            builder.SetMinimumLevel(minLevel);
        });
    }

    /// <summary>
    /// Creates a logger for the specified type.
    /// </summary>
    /// <typeparam name="T">The type for which to create a logger.</typeparam>
    /// <returns>An ILogger instance.</returns>
    public ILogger<T> CreateLogger<T>()
    {
        return _loggerFactory.CreateLogger<T>();
    }

    /// <summary>
    /// Creates a logger with the specified category name.
    /// </summary>
    /// <param name="categoryName">The category name for the logger.</param>
    /// <returns>An ILogger instance.</returns>
    public ILogger CreateLogger(string categoryName)
    {
        return _loggerFactory.CreateLogger(categoryName);
    }

    /// <summary>
    /// Clears all captured log entries.
    /// </summary>
    public void ClearLogs()
    {
        _loggerProvider.Clear();
    }

    /// <summary>
    /// Gets log entries filtered by log level.
    /// </summary>
    /// <param name="level">The log level to filter by.</param>
    /// <returns>Filtered log entries.</returns>
    public IReadOnlyList<LogEntry> GetLogs(LogLevel level)
    {
        return LogEntries.Where(e => e.Level == level).ToList();
    }

    /// <summary>
    /// Gets log entries filtered by category.
    /// </summary>
    /// <param name="category">The category to filter by.</param>
    /// <returns>Filtered log entries.</returns>
    public IReadOnlyList<LogEntry> GetLogs(string category)
    {
        return LogEntries.Where(e => e.Category.Equals(category, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    /// <summary>
    /// Gets log entries matching a predicate.
    /// </summary>
    /// <param name="predicate">The filter predicate.</param>
    /// <returns>Filtered log entries.</returns>
    public IReadOnlyList<LogEntry> GetLogs(Func<LogEntry, bool> predicate)
    {
        return LogEntries.Where(predicate).ToList();
    }

    /// <summary>
    /// Checks if a log entry with the specified message was logged.
    /// </summary>
    /// <param name="message">The message to search for.</param>
    /// <returns>True if the message was logged, false otherwise.</returns>
    public bool ContainsLog(string message)
    {
        return LogEntries.Any(e => e.Message.Contains(message, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Asserts that a log with the specified level and message was written.
    /// </summary>
    /// <param name="level">Expected log level.</param>
    /// <param name="message">Expected message (partial match).</param>
    public void AssertLogged(LogLevel level, string message)
    {
        var found = LogEntries.Any(e => 
            e.Level == level && 
            e.Message.Contains(message, StringComparison.OrdinalIgnoreCase));

        if (!found)
        {
            throw new InvalidOperationException(
                $"Expected to find log with level {level} containing '{message}', but none was found. " +
                $"Captured {LogEntries.Count} log entries.");
        }
    }

    /// <summary>
    /// Asserts that a log containing the specified message was written.
    /// </summary>
    /// <param name="message">Expected message (partial match).</param>
    public void AssertLoggedContaining(string message)
    {
        if (!ContainsLog(message))
        {
            throw new InvalidOperationException(
                $"Expected to find log containing '{message}', but none was found. " +
                $"Captured {LogEntries.Count} log entries.");
        }
    }

    /// <summary>
    /// Asserts the exact number of log entries captured.
    /// </summary>
    /// <param name="expectedCount">Expected number of log entries.</param>
    public void AssertLogCount(int expectedCount)
    {
        if (LogEntries.Count != expectedCount)
        {
            throw new InvalidOperationException(
                $"Expected {expectedCount} log entries, but found {LogEntries.Count}.");
        }
    }

    /// <summary>
    /// Asserts that no error or critical logs were written.
    /// </summary>
    public void AssertNoErrors()
    {
        var errors = GetLogs(LogLevel.Error).Concat(GetLogs(LogLevel.Critical)).ToList();
        if (errors.Any())
        {
            throw new InvalidOperationException(
                $"Expected no errors, but found {errors.Count} error/critical logs.");
        }
    }

    /// <summary>
    /// Asserts that a warning with the specified message was logged.
    /// </summary>
    /// <param name="message">Expected message (partial match).</param>
    public void AssertWarningLogged(string message)
    {
        AssertLogged(LogLevel.Warning, message);
    }

    /// <summary>
    /// Disposes the fixture and releases resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _loggerFactory.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Represents a captured log entry.
/// </summary>
public class LogEntry
{
    /// <summary>
    /// Gets the log level.
    /// </summary>
    public LogLevel Level { get; set; }

    /// <summary>
    /// Gets the logger category.
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Gets the formatted log message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets the timestamp when the log was written.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>
    /// Gets the exception associated with the log, if any.
    /// </summary>
    public Exception? Exception { get; set; }

    /// <summary>
    /// Gets the event ID.
    /// </summary>
    public EventId EventId { get; set; }
}

/// <summary>
/// Test logger provider that captures log messages for verification.
/// </summary>
internal class TestLoggerProvider : ILoggerProvider
{
    private readonly LogLevel _minLevel;
    private ConcurrentBag<LogEntry> _logEntries;

    public IReadOnlyList<LogEntry> LogEntries => _logEntries.ToList();

    public TestLoggerProvider(LogLevel minLevel)
    { _minLevel = minLevel;
        _logEntries = new ConcurrentBag<LogEntry>();
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new TestLogger(categoryName, _minLevel, _logEntries);
    }

    public void Clear()
    {
        _logEntries = new ConcurrentBag<LogEntry>();
    }

    public void Dispose()
    {
    }
}

/// <summary>
/// Test logger implementation that captures log messages.
/// </summary>
internal class TestLogger : ILogger
{
    private readonly string _category;
    private readonly LogLevel _minLevel;
    private readonly ConcurrentBag<LogEntry> _logEntries;

    public TestLogger(string category, LogLevel minLevel, ConcurrentBag<LogEntry> logEntries)
    {
        _category = category;
        _minLevel = minLevel;
        _logEntries = logEntries;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return null;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return logLevel >= _minLevel;
    }

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        var message = formatter(state, exception);

        _logEntries.Add(new LogEntry
        {
            Level = logLevel,
            Category = _category,
            Message = message,
            Timestamp = DateTimeOffset.UtcNow,
            Exception = exception,
            EventId = eventId
        });
    }
}
