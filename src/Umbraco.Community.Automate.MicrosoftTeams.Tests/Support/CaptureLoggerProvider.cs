using Microsoft.Extensions.Logging;

namespace Umbraco.Community.Automate.MicrosoftTeams.Tests.Support;

/// <summary>Captures every log entry: level, category, formatted message, the state's key/value pairs and the exception text.</summary>
internal sealed class CaptureLoggerProvider : ILoggerProvider
{
    public List<(LogLevel Level, string Category, string Text)> Entries { get; } = [];

    public ILogger CreateLogger(string categoryName) => new CaptureLogger(this, categoryName);

    public void Dispose()
    {
    }

    public IEnumerable<string> AllText => Entries.Select(e => e.Text);

    private sealed class CaptureLogger(CaptureLoggerProvider owner, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var pairs = state is IEnumerable<KeyValuePair<string, object?>> kv ? string.Join(";", kv.Select(p => $"{p.Key}={p.Value}")) : string.Empty;
            var text = $"{formatter(state, exception)} | {pairs} | {exception}";
            lock (owner.Entries)
            {
                owner.Entries.Add((logLevel, category, text));
            }
        }
    }
}
