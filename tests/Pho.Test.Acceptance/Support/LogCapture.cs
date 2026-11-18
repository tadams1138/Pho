using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Pho.Test.Acceptance.Support;

public sealed record CapturedLog(string Category, LogLevel Level, string Message);

/// <summary>Collects what Pho writes to its log, so scenarios can check what an operator would see.</summary>
public sealed class LogCapture : ILoggerProvider
{
    private readonly ConcurrentQueue<CapturedLog> _entries = new();

    public IReadOnlyList<CapturedLog> Entries => _entries.ToList();

    public ILogger CreateLogger(string categoryName) => new Logger(categoryName, _entries);

    public void Dispose()
    {
    }

    private sealed class Logger(string category, ConcurrentQueue<CapturedLog> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel)) entries.Enqueue(new CapturedLog(category, logLevel, formatter(state, exception)));
        }
    }
}
