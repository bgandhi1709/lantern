using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Lantern.Api.Tests.Infrastructure;

public sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<string> entries = new();

    public IReadOnlyCollection<string> Entries => [.. this.entries];

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(this.entries);

    public void Dispose()
    {
    }

    private sealed class CapturingLogger(ConcurrentQueue<string> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        )
        {
            entries.Enqueue(formatter(state, exception));

            if (exception is not null)
            {
                entries.Enqueue(exception.ToString());
            }
        }
    }
}
