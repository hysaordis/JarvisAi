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
