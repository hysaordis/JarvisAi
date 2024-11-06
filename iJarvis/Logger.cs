using Jarvis.Ai.Core.Events;
using Jarvis.Ai.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using System.Text.Json;

public class CustomConsoleFormatter : ConsoleFormatter
{
    public CustomConsoleFormatter() : base("CustomFormat") { }

    public override void Write<TState>(
        in LogEntry<TState> logEntry,
        IExternalScopeProvider scopeProvider,
        TextWriter textWriter)
    {
        string message = logEntry.Formatter(logEntry.State, logEntry.Exception);
        textWriter.WriteLine(message);
        
        var logEvent = new LogEvent(message, logEntry.LogLevel);
        EventBus.Instance.Publish(logEvent);
    }
}
public class Logger : IJarvisLogger
{
    private readonly ILogger _logger;

    #region Constants - Emoji Mappings
    private static readonly Dictionary<string, string> DeviceEmojis = new()
    {
        ["initialized"] = "🔌",
        ["starting"] = "🟢",
        ["stopping"] = "🔴",
        ["ready"] = "✅",
        ["error"] = "⚠️",
        ["busy"] = "⏳"
    };

    private static readonly Dictionary<string, string> TranscriptionEmojis = new()
    {
        ["starting"] = "🎯",
        ["listening"] = "👂",
        ["processing"] = "⚡",
        ["buffering"] = "💫",
        ["completed"] = "✨",
        ["canceled"] = "🚫"
    };

    private static readonly Dictionary<string, string> AgentEmojis = new()
    {
        ["initializing"] = "🌟",
        ["ready"] = "✨",
        ["thinking"] = "🤔",
        ["processing"] = "⚡",
        ["listening"] = "👂",
        ["speaking"] = "🗣️",
        ["error"] = "❌",
        ["shutdown"] = "💤"
    };

    private static readonly Dictionary<string, string> ConversationEmojis = new()
    {
        ["user"] = "👤",
        ["assistant"] = "🤖",
        ["system"] = "⚙️",
        ["tool"] = "🛠️"
    };

    private static readonly Dictionary<string, string> MemoryEmojis = new()
    {
        ["store"] = "💾",
        ["retrieve"] = "📤",
        ["clear"] = "🧹",
        ["update"] = "🔄"
    };
    #endregion

    public Logger()
    {
        var serviceProvider = new ServiceCollection()
            .AddLogging(builder =>
            {
                builder.AddConsole(options => options.FormatterName = "CustomFormat");
                builder.AddConsoleFormatter<CustomConsoleFormatter, ConsoleFormatterOptions>();
            })
            .BuildServiceProvider();

        _logger = serviceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger<Logger>();
    }

    #region Base Logging Methods
    public void LogInformation(string message, params object[] args) => _logger.LogInformation(message, args);
    public void LogError(string message, params object[] args) => _logger.LogError(message, args);
    public void LogWarning(string message, params object[] args) => _logger.LogWarning(message, args);
    public void LogDebug(string message, params object[] args) => _logger.LogDebug(message, args);
    #endregion

    #region Voice and Transcription Logging
    public void LogVoiceActivity(bool isActive, float level = 0)
    {
        var emoji = isActive
            ? GetVoiceEmoji(level)
            : "🤫";

        var message = isActive
            ? $"{emoji} Voice Detected [Level: {level:F2}]"
            : $"{emoji} Silence";

        _logger.LogInformation(message);
    }

    public void LogTranscriptionProgress(string status, string details = "")
    {
        var emoji = TranscriptionEmojis.GetValueOrDefault(status.ToLower(), "🎤");
        LogWithDetails(emoji, status, details);
    }

    public void LogTranscriptionResult(string text, float confidence = 1.0f)
    {
        var emoji = GetConfidenceEmoji(confidence);
        _logger.LogInformation($"{emoji} Transcription [{confidence:P0}]: {text}");
    }

    public void LogDeviceStatus(string status, string details = "")
    {
        var emoji = DeviceEmojis.GetValueOrDefault(status.ToLower(), "🎙️");
        LogWithDetails(emoji, status, details);
    }

    public void LogTranscriberError(string component, string error)
    {
        _logger.LogError($"❌ {component} Error: {error}");
    }
    #endregion

    #region Agent Logging
    public void LogAgentStatus(string status, string details = "")
    {
        var emoji = AgentEmojis.GetValueOrDefault(status.ToLower(), "🤖");
        LogWithDetails(emoji, status, details);
    }

    public void LogConversation(string role, string content, string details = "")
    {
        var emoji = ConversationEmojis.GetValueOrDefault(role.ToLower(), "💭");
        var message = string.IsNullOrEmpty(details)
            ? $"{emoji} {role}: {content}"
            : $"{emoji} {role}: {content} | {details}";
        _logger.LogInformation(message);
    }

    public void LogThinking(string thought, string details = "")
    {
        LogWithDetails("🤔", thought, details);
    }

    public void LogMemory(string action, string details = "")
    {
        var emoji = MemoryEmojis.GetValueOrDefault(action.ToLower(), "💭");
        LogWithDetails(emoji, action, details);
    }

    public void LogToolExecution(string toolName, string status, string details = "")
    {
        var emoji = status.ToLower() switch
        {
            "start" => "🔧",
            "success" => "✅",
            "error" => "⚠️",
            "complete" => "🏁",
            _ => "🛠️"
        };

        var message = string.IsNullOrEmpty(details)
            ? $"{emoji} {toolName}: {status}"
            : $"{emoji} {toolName}: {status} | {details}";

        _logger.LogInformation(message);
    }
    #endregion

    #region Event Logging
    public void LogWsEvent(string direction, dynamic eventObj)
    {
        try
        {
            string eventType = eventObj.type ?? "unknown";
            if (eventType == "error")
            {
                string errorMessage = eventObj.error?.message ?? "No error message provided.";
                _logger.LogError($"[{direction}] Error Event: {errorMessage}");
            }
            else
            {
                _logger.LogInformation($"{GetEventEmoji(eventType)} - {direction}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"[{direction}] Error logging event: {ex.Message}");
        }
    }

    public void LogToolCall(string functionName, object args, object result)
    {
        _logger.LogInformation($"🛠️ Calling function: {functionName} with args: {JsonSerializer.Serialize(args)}");
        _logger.LogInformation($"🛠️ Function call result: {JsonSerializer.Serialize(result)}");
    }
    #endregion

    #region ILogger Implementation
    public IDisposable BeginScope<TState>(TState state) where TState : notnull => _logger.BeginScope(state);
    public bool IsEnabled(LogLevel logLevel) => _logger.IsEnabled(logLevel);
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
        => _logger.Log(logLevel, eventId, state, exception, formatter);
    #endregion

    #region Helper Methods
    private string GetVoiceEmoji(float level) => level switch
    {
        < 0.2f => "🔈",
        < 0.5f => "🔉",
        _ => "🔊"
    };

    private string GetConfidenceEmoji(float confidence) => confidence switch
    {
        >= 0.9f => "📝",
        >= 0.7f => "📜",
        >= 0.5f => "📄",
        _ => "❓"
    };

    private void LogWithDetails(string emoji, string status, string details)
    {
        var message = string.IsNullOrEmpty(details)
            ? $"{emoji} {status}"
            : $"{emoji} {status}: {details}";
        _logger.LogInformation(message);
    }

    private string GetEventEmoji(string eventType) => eventType switch
    {
        "session.created" => "🔌",
        "session.updated" => "🔄",
        "session.error" => "⚠️",
        "voice.detected" => "🗣️",
        "transcription.completed" => "✨",
        "transcription.error" => "📝❌",
        _ => "❓"
    };
    #endregion
}