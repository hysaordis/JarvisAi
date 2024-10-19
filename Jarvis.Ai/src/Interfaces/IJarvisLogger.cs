using Microsoft.Extensions.Logging;

namespace Jarvis.Ai.src.Interfaces;

public interface IJarvisLogger
{
    void LogInformation(string message, params object[] args);
    void LogError(string message, params object[] args);
    void LogWarning(string message, params object[] args);
    void LogDebug(string message, params object[] args);
    void LogWsEvent(string direction, dynamic eventObj);
    void LogToolCall(string functionName, object args, object result);
    IDisposable BeginScope<TState>(TState state) where TState : notnull;
    bool IsEnabled(LogLevel logLevel);
    void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter);
}