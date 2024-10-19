using System.Text.Json;
using Jarvis.Ai.src.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Jarvis.Console;
public class Logger : IJarvisLogger
{
    private readonly ILogger _logger;

    public Logger()
    {
        var serviceProvider = new ServiceCollection()
            .AddLogging(builder => builder.AddConsole())
            .BuildServiceProvider();

        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger<Logger>();
    }

    public void LogInformation(string message, params object[] args)
    {
        _logger.LogInformation(message, args);
    }

    public void LogError(string message, params object[] args)
    {
        _logger.LogError(message, args);
    }

    public void LogWarning(string message, params object[] args)
    {
        _logger.LogWarning(message, args);
    }

    public void LogDebug(string message, params object[] args)
    {
        _logger.LogDebug(message, args);
    }

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
                _logger.LogInformation($"{GetEmojiForEventType(eventType)} - {direction}");
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

    private string GetEmojiForEventType(string eventType)
    {
        var eventEmojis = new Dictionary<string, string>
        {
            { "session.update", "🛠️" },
            { "session.created", "🔌" },
            { "session.updated", "🔄" },
            { "input_audio_buffer.append", "🎤" },
            { "input_audio_buffer.commit", "✅" },
            { "input_audio_buffer.speech_started", "🗣️" },
            { "input_audio_buffer.speech_stopped", "🤫" },
            { "input_audio_buffer.cleared", "🧹" },
            { "input_audio_buffer.committed", "📨" },
            { "conversation.item.create", "📥" },
            { "conversation.item.delete", "🗑️" },
            { "conversation.item.truncate", "✂️" },
            { "conversation.item.created", "📤" },
            { "conversation.item.deleted", "🗑️" },
            { "conversation.item.truncated", "✂️" },
            { "response.create", "➡️" },
            { "response.created", "📝" },
            { "response.output_item.added", "➕" },
            { "response.output_item.done", "✅" },
            { "response.text.delta", "✍️" },
            { "response.text.done", "📝" },
            { "response.audio.delta", "🔊" },
            { "response.audio.done", "🔇" },
            { "response.done", "✔️" },
            { "response.cancel", "⛔" },
            { "response.function_call_arguments.delta", "📥" },
            { "response.function_call_arguments.done", "📥" },
            { "rate_limits.updated", "⏳" },
            { "error", "❌" },
            { "conversation.item.input_audio_transcription.completed", "📝" },
            { "conversation.item.input_audio_transcription.failed", "⚠️" },
        };
        return eventEmojis.TryGetValue(eventType, out var emoji) ? emoji : "❓";
    }

    public IDisposable BeginScope<TState>(TState state) where TState : notnull => _logger.BeginScope(state);

    public bool IsEnabled(LogLevel logLevel) => _logger.IsEnabled(logLevel);

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        _logger.Log(logLevel, eventId, state, exception, formatter);
    }
}