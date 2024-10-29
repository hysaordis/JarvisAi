using Microsoft.Extensions.Logging;

namespace Jarvis.Ai.Interfaces;

public interface IJarvisLogger
{
    void LogInformation(string message, params object[] args);
    void LogError(string message, params object[] args);
    void LogWarning(string message, params object[] args);
    void LogDebug(string message, params object[] args);

    // logging transcriber
    void LogVoiceActivity(bool isActive, float level = 0);
    void LogTranscriptionProgress(string status, string details = "");
    void LogTranscriptionResult(string text, float confidence = 1.0f);
    void LogDeviceStatus(string status, string details = "");
    void LogTranscriberError(string component, string error);

    void LogWsEvent(string direction, dynamic eventObj);
    void LogToolCall(string functionName, object args, object result);

    // ILogger

    // Agent
    void LogAgentStatus(string status, string details = "");
    void LogConversation(string role, string content, string details = "");
    void LogToolExecution(string toolName, string status, string details = "");
    void LogThinking(string thought, string details = "");
    void LogMemory(string action, string details = "");

    IDisposable BeginScope<TState>(TState state) where TState : notnull;
    bool IsEnabled(LogLevel logLevel);
    void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter);
}