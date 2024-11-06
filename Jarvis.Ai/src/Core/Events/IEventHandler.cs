using Microsoft.Extensions.Logging;

namespace Jarvis.Ai.Core.Events;

public interface IEvent
{
    string Type { get; }
    DateTime Timestamp { get; }
}

public interface IEventHandler<in TEvent> where TEvent : IEvent
{
    Task HandleAsync(TEvent @event);
}

public class LogEvent : IEvent
{
    public string Type => "log";
    public DateTime Timestamp { get; } = DateTime.UtcNow;
    public string Message { get; }
    public LogLevel LogLevel { get; }

    public LogEvent(string message, LogLevel logLevel)
    {
        Message = message;
        LogLevel = logLevel;
    }
}

public class ChatEvent : IEvent
{
    public string Type => "chat";
    public DateTime Timestamp { get; } = DateTime.UtcNow;
    public string Message { get; }

    public ChatEvent(string message)
    {
        Message = message;
    }
}