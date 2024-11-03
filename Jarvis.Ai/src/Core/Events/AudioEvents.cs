namespace Jarvis.Ai.Core.Events;

public enum SystremState
{
    Idle,
    Listening,
    Playing,
    Processing,
    ExecutingFunction
}

public interface IAudioEvent : IEvent
{
    string Error { get; }
}

public record SystemStateEvent : IAudioEvent
{
    public string Type => "audio.state";
    public DateTime Timestamp { get; } = DateTime.UtcNow;
    public string State { get; init; }
    public string Error { get; init; }
}

public record AudioInputLevelEvent : IAudioEvent
{
    public string Type => "audio.input.level";
    public DateTime Timestamp { get; } = DateTime.UtcNow;
    public float Level { get; init; }
    public string Error { get; init; }
}