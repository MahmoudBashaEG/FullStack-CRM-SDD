using Microsoft.Extensions.Logging;
namespace Crm.IntegrationTests.Fixtures;

public sealed class AuthLogCapture : ILoggerProvider
{
    public List<string> Messages { get; } = [];
    public ILogger CreateLogger(string categoryName) => new Capture(Messages);
    public void Dispose() { }
    private sealed class Capture(List<string> messages) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception, Func<TState,Exception?,string> formatter)
        { lock(messages) messages.Add(formatter(state,exception)); }
    }
}
