using Microsoft.AspNetCore.SignalR;
using Jarvis.Ai;
using Jarvis.Ai.Core.Events;
using Jarvis.Ai.Interfaces;

namespace Jarvis.Service.Hubs;

public class AlitaHub : Hub
{
    private readonly IJarvisLogger _logger;
    private readonly AlitaAgent _alitaAgent;
    private readonly IHubContext<AlitaHub> _hubContext;

    public AlitaHub(IJarvisLogger logger, AlitaAgent alitaAgent, IHubContext<AlitaHub> hubContext)
    {
        _logger = logger;
        _alitaAgent = alitaAgent;
        _hubContext = hubContext;

        EventBus.Instance.Subscribe<AudioInputLevelEvent>(async evt =>
        {
            await Clients.All.SendAsync("AudioEvent", new
            {
                type = evt.Type,
                level = evt.Level,
                error = evt.Error,
                timestamp = evt.Timestamp
            });
        });

        EventBus.Instance.Subscribe<SystemStateEvent>(async evt =>
        {
            await Clients.All.SendAsync("AudioEvent", new
            {
                type = evt.Type,
                state = evt.State.ToLower(),
                error = evt.Error,
                timestamp = evt.Timestamp
            });
        });

        EventBus.Instance.Subscribe<LogEvent>(async evt =>
        {
            await Clients.All.SendAsync("LogEvent", new
            {
                type = evt.Type,
                message = evt.Message,
                logLevel = evt.LogLevel.ToString(),
                timestamp = evt.Timestamp
            });
        });

        EventBus.Instance.Subscribe<ChatEvent>(async evt =>
        {
            await Clients.All.SendAsync("ChatEvent", new
            {
                type = evt.Type,
                message = evt.Message,
                timestamp = evt.Timestamp
            });
        });
    }

    public async Task StartupAsync()
    {
        try
        {
            await _alitaAgent.StartupAsync(CancellationToken.None);
            _logger.LogInformation("Alita Agent started successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to start Alita: {ex.Message}");
            throw;
        }
    }

    public async Task StartListeningAsync()
    {
        try
        {
            await _alitaAgent.StartListeningAsync(CancellationToken.None);
            _logger.LogInformation("Alita is now listening");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to start listening: {ex.Message}");
            throw;
        }
    }

    public async Task StopListeningAsync()
    {
        try
        {
            await _alitaAgent.StopListeningAsync(CancellationToken.None);
            _logger.LogInformation("Alita stopped listening");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to stop listening: {ex.Message}");
            throw;
        }
    }

    public async Task ChatAsync(string message)
    {
        try
        {
            // Send acknowledgment that message was received
            await Clients.Caller.SendAsync("ChatEvent", new
            {
                type = "chat",
                message = "Processing your message...",
                timestamp = DateTime.UtcNow
            });

            // Process the chat message
            await _alitaAgent.ChatAsync(message);

        }
        catch (Exception ex)
        {
            _logger.LogError($"Chat processing failed: {ex.Message}");
            
            // Send error message back to client
            await Clients.Caller.SendAsync("ChatEvent", new
            {
                type = "chat",
                message = "Sorry, I encountered an error processing your message.",
                timestamp = DateTime.UtcNow
            });
            
            throw;
        }
    }

    public async Task<string> GetServiceStatus()
    {
        return "Connected";
    }

    public async Task InitializeServiceAsync()
    {
        try
        {

            await _alitaAgent.StartupAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Service initialization failed: {ex.Message}");
            throw;
        }
    }

    public async Task HeartbeatAsync()
    {
        await Clients.Caller.SendAsync("HeartbeatResponse", DateTime.UtcNow);
    }

    public async Task SetVoiceMuteAsync(bool mute)
    {
        try
        {
            _alitaAgent.SetMute(mute);
            await Clients.All.SendAsync("VoiceMuteStateChanged", new
            {
                muted = mute,
                timestamp = DateTime.UtcNow
            });
            _logger.LogInformation($"Voice output {(mute ? "muted" : "unmuted")}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to {(mute ? "mute" : "unmute")} voice output: {ex.Message}");
            throw;
        }
    }

    public bool GetVoiceMuteState()
    {
        return _alitaAgent.IsMuted();
    }

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
    }
}

public enum ServiceStatus
{
    Disconnected,
    Initializing,
    Connected,
    Error,
    Reconnecting
}
