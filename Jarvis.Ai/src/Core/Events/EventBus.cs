namespace Jarvis.Ai.Core.Events;
public class EventBus
{
    private readonly Dictionary<Type, List<Delegate>> _handlers = new();
    private readonly object _lock = new();
    private static readonly Lazy<EventBus> _instance = new(() => new EventBus());

    public static EventBus Instance => _instance.Value;

    public void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : IEvent
    {
        lock (_lock)
        {
            var eventType = typeof(TEvent);
            if (!_handlers.ContainsKey(eventType))
            {
                _handlers[eventType] = new List<Delegate>();
            }
            _handlers[eventType].Add(handler);
        }
    }

    public void Publish<TEvent>(TEvent @event) where TEvent : IEvent
    {
        var eventType = @event.GetType();
        if (_handlers.TryGetValue(eventType, out var handlers))
        {
            foreach (var handler in handlers)
            {
                if (handler is Action<TEvent> typedHandler)
                {
                    typedHandler(@event);
                }
            }
        }
    }
}
