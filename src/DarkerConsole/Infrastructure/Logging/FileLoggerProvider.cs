using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace DarkerConsole.Infrastructure.Logging;

internal sealed class FileLoggerProvider(string logDirectory, int retainedFileCount) : ILoggerProvider
{
    private readonly ConcurrentDictionary<string, FileLogger> loggers = new();
    private readonly Lock fileLock = new();
    private string? currentLogPath;
    private StreamWriter? currentWriter;
    private bool disposed;

    public ILogger CreateLogger(string categoryName) =>
        loggers.GetOrAdd(categoryName, name => new FileLogger(name, this));

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        foreach (var logger in loggers.Values)
            logger.Dispose();

        using var _ = fileLock.EnterScope();
        currentWriter?.Dispose();
        currentWriter = null;
        currentLogPath = null;
    }

    internal void WriteLog(
        string categoryName,
        LogLevel logLevel,
        EventId eventId,
        string message,
        Exception? exception
    )
    {
        if (disposed)
            return;

        var timestamp = DateTimeOffset.Now.ToString("O");
        var builder = new StringBuilder()
            .Append(timestamp)
            .Append(' ')
            .Append('[')
            .Append(logLevel)
            .Append(']')
            .Append(' ')
            .Append(categoryName)
            .Append(':')
            .Append(' ')
            .Append(message);

        if (exception != null)
        {
            builder
                .AppendLine()
                .Append(exception);
        }

        using var _ = fileLock.EnterScope();
        EnsureWriter();
        currentWriter?.WriteLine(builder.ToString());
    }

    private void EnsureWriter()
    {
        if (disposed)
            return;

        var todayPath = Path.Combine(logDirectory, $"{DateTime.UtcNow:yyyyMMdd}.log");

        if (!Directory.Exists(logDirectory))
            Directory.CreateDirectory(logDirectory);

        if (!string.Equals(currentLogPath, todayPath, StringComparison.OrdinalIgnoreCase))
        {
            currentWriter?.Dispose();
            currentWriter = new StreamWriter(
                new FileStream(
                    todayPath,
                    FileMode.Append,
                    FileAccess.Write,
                    FileShare.Read
                )
            )
            {
                AutoFlush = true,
                NewLine = Environment.NewLine,
            };
            currentLogPath = todayPath;
            CleanupOldLogFiles();
        }
    }

    private void CleanupOldLogFiles()
    {
        try
        {
            if (!Directory.Exists(logDirectory))
                return;

            var files = new DirectoryInfo(logDirectory)
                .EnumerateFiles("*.log")
                .OrderByDescending(f => f.Name)
                .Skip(GetRetentionLimit())
                .ToList();

            foreach (var file in files)
            {
                try
                {
                    file.Delete();
                }
                catch
                {
                    // Ignore failures to clean up old logs
                }
            }
        }
        catch
        {
            // Ignore errors during cleanup
        }
    }

    private sealed class FileLogger(string categoryName, FileLoggerProvider provider) : ILogger, IDisposable
    {
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        )
        {
            if (formatter == null)
                throw new ArgumentNullException(nameof(formatter));

            var message = formatter(state, exception);
            provider.WriteLog(categoryName, logLevel, eventId, message, exception);
        }

        public void Dispose()
        {
            // Nothing to dispose individually. The provider handles shared resources.
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }

    private int GetRetentionLimit() => retainedFileCount > 0 ? retainedFileCount : 7;
}
