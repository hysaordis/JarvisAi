using System.Collections.Concurrent;
using System.Threading.Tasks.Dataflow;
using Jarvis.Ai.Common.Settings;
using Jarvis.Ai.Core.Events;
using Jarvis.Ai.Interfaces;
using Jarvis.Ai.LLM;
using Jarvis.Ai.Persistence;
using Newtonsoft.Json;

namespace Jarvis.Ai;

/// <summary>
/// Represents the main agent responsible for handling transcription, processing commands, and interacting with various modules.
/// </summary>
public class AlitaAgent : IDisposable
{
    #region Private Fields
    private readonly ILlmClient _llmClient;
    private readonly IJarvisLogger _logger;
    private readonly IModuleRegistry _moduleRegistry;
    private readonly ITranscriber _transcriptionService;
    private readonly IAudioOutputModule _audioOutputModule;
    private readonly ConcurrentDictionary<string, byte> _processedTranscriptions = new();
    private readonly StarkProtocols _starkProtocols;
    private bool _isDisposed;
    private CancellationTokenSource _listeningCts;
    private CancellationTokenSource? _processingCts;
    private volatile bool _isProcessing;
    private readonly IConversationStore _conversationStore;
    private volatile bool _isMuted;

    #endregion

    #region Constructor
    /// <summary>
    /// Initializes a new instance of the <see cref="AlitaAgent"/> class.
    /// </summary>
    /// <param name="logger">The logger instance for logging information.</param>
    /// <param name="moduleRegistry">The module registry for executing commands.</param>
    /// <param name="transcriptionService">The transcription service for handling audio input.</param>
    /// <param name="audioOutputModule">The audio output module for speaking responses.</param>
    /// <param name="llmClient">The LLM client for processing commands.</param>
    /// <param name="starkProtocols">The protocols containing session instructions.</param>
    /// <param name="conversationStore">The store for saving and retrieving conversation history.</param>
    public AlitaAgent(
        IJarvisLogger logger,
        IModuleRegistry moduleRegistry,
        ITranscriber transcriptionService,
        IAudioOutputModule audioOutputModule,
        ILlmClient llmClient,
        StarkProtocols starkProtocols,
        IConversationStore conversationStore)
    {
        _logger = logger;
        _moduleRegistry = moduleRegistry;
        _transcriptionService = transcriptionService;
        _audioOutputModule = audioOutputModule;
        _llmClient = llmClient;
        _starkProtocols = starkProtocols;

        //_conversationHistory = new List<Message>();

        _transcriptionService.OnTranscriptionResult += HandleTranscriptionResult;
        _transcriptionService.PartialTranscriptReceived += HandlePartialTranscriptionResult;

        _conversationStore = conversationStore;
    }

    #endregion

    #region Public Interface
    /// <summary>
    /// Initializes Alita and starts the core systems.
    /// </summary>
    public async Task StartupAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogAgentStatus("initializing", "Starting Alita Agent system");

            await _transcriptionService.InitializeAsync(cancellationToken);
            await InitializeSystemPromptAsync(cancellationToken); // Riabilitato

            _logger.LogAgentStatus("ready", "Alita Agent initialized and ready");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Initialization failed: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Stops the listening process and cancels current processing.
    /// </summary>
    public async Task StopListeningAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogAgentStatus("stopping", "Stopping listening process");

            // Stop transcription service
            _transcriptionService.StopListening();

            // Cancel current processing if any
            CancelCurrentProcessing();

            await Task.CompletedTask;

            // Event bus
            EventBus.Instance.Publish(new SystemStateEvent() { State = SystremState.Idle.ToString() });

            _logger.LogAgentStatus("stopped", "Listening process stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error during stop listening operation: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Shuts down Alita and cleans up resources.
    /// </summary>
    public async Task ShutdownAsync()
    {
        _logger.LogAgentStatus("shutdown", "Initiating shutdown sequence");
        _transcriptionService.StopListening();
        _listeningCts?.Cancel();
        await Task.CompletedTask;
    }
    #endregion

    #region Audio Processing
    /// <summary>
    /// Starts listening for audio input asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation if needed.</param>
    public async Task StartListeningAsync(CancellationToken cancellationToken)
    {
        _listeningCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _transcriptionService.StartListening();
        EventBus.Instance.Publish(new SystemStateEvent() { State = SystremState.Listening.ToString() });
        _logger.LogInformation("Started listening");
    }

    private void HandleTranscriptionResult(object sender, string transcription)
    {
        if (string.IsNullOrEmpty(transcription)) return;

        if (_isProcessing)
        {
            _logger.LogThinking("New input received, canceling current processing");
            CancelCurrentProcessing();
        }

        if (!_processedTranscriptions.ContainsKey(transcription))
        {
            _logger.LogConversation("transcription", transcription, "New input detected");
            // Invece di usare il buffer, processiamo direttamente
            Task.Run(() => ProcessCommandAsync(transcription, _listeningCts.Token));
        }
        else
        {
            _logger.LogThinking("Skipping duplicate transcription", transcription);
        }
    }

    public async Task ChatAsync(string request)
    {
        if (string.IsNullOrEmpty(request)) return;

        if (_listeningCts == null)
        {
            _listeningCts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        }

        if (_isProcessing)
        {
            _logger.LogThinking("New input received, canceling current processing");
            CancelCurrentProcessing();
        }

        if (!_processedTranscriptions.ContainsKey(request))
        {
            _logger.LogConversation("transcription", request, "New input detected");
            await ProcessCommandAsync(request, _listeningCts.Token);
        }
        else
        {
            _logger.LogThinking("Skipping duplicate transcription", request);
        }
    }

    private void HandlePartialTranscriptionResult(object? sender, string e)
    {
        CancelCurrentProcessing();
    }
    #endregion

    #region Command Processing
    /// <summary>
    /// Processes a command asynchronously.
    /// </summary>
    /// <param name="command">The command to process.</param>
    /// <param name="externalCancellationToken">Token to cancel the operation if needed.</param>
    private async Task ProcessCommandAsync(string command, CancellationToken externalCancellationToken)
    {
        if (_isProcessing)
        {
            _logger.LogInformation("Already processing a command, skipping...");
            return;
        }

        try
        {
            EventBus.Instance.Publish(new SystemStateEvent() { State = SystremState.Processing.ToString() });

            _isProcessing = true;

            _processingCts = CancellationTokenSource.CreateLinkedTokenSource(externalCancellationToken);
            var processingToken = _processingCts.Token;

            _logger.LogThinking($"Processing user request: {command}");
            await _conversationStore.SaveMessageAsync(new Message { Role = "user", Content = command });

            while (!processingToken.IsCancellationRequested)
            {
                var response = await _llmClient.SendCommandToLlmAsync(null, processingToken);
                _logger.LogConversation("assistant", response.Content);

                if (response.ToolCalls == null || !string.IsNullOrEmpty(response.Content))
                {
                    EventBus.Instance.Publish(new ChatEvent(response.Content));
                    if (!_isMuted)
                    {
                        await _audioOutputModule.SpeakAsync(response.Content, processingToken);
                    }
                    else
                    {
                        EventBus.Instance.Publish(new SystemStateEvent
                        {
                            State = SystremState.Listening.ToString()
                        });
                    }
                    break;
                }

                foreach (var toolCall in response.ToolCalls)
                {
                    await ExecuteToolCallAsync(toolCall, processingToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogAgentStatus("canceled", "Processing canceled");
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex, externalCancellationToken);
        }
        finally
        {
            _isProcessing = false;
            _processingCts?.Dispose();
            _processingCts = null;
        }
    }

    /// <summary>
    /// Executes a tool call asynchronously.
    /// </summary>
    /// <param name="functionCall">The function call to execute.</param>
    /// <param name="cancellationToken">Token to cancel the operation if needed.</param>
    private async Task ExecuteToolCallAsync(FunctionCall functionCall, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogToolExecution(functionCall.Function.Name, "start",
            $"Executing with args: {functionCall.Function.Arguments}");

            EventBus.Instance.Publish(new SystemStateEvent() { State = SystremState.ExecutingFunction.ToString() });

            var funcArgs = JsonConvert.SerializeObject(functionCall.Function.Arguments);
            var args = JsonConvert.DeserializeObject<Dictionary<string, object>>(funcArgs);
            var result = await _moduleRegistry.ExecuteCommand(functionCall.Function.Name, args);
            var functionResponse = JsonConvert.SerializeObject(result);

            _logger.LogToolExecution(functionCall.Function.Name, "complete",
            $"Completed with result: {functionResponse}");

            await _conversationStore.SaveMessageAsync(new Message
            {
                Role = "tool",
                Content = functionResponse,
                ToolCallId = functionCall.Id
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error executing tool call {functionCall.Function.Name}: {ex.Message}");
            await _conversationStore.SaveMessageAsync(new Message
            {
                Role = "tool",
                Content = $"Error: {ex.Message}",
                ToolCallId = functionCall.Id
            });
        }
    }

    /// <summary>
    /// Cancels the current processing.
    /// </summary>
    private void CancelCurrentProcessing()
    {
        try
        {
            if (_processingCts != null)
            {
                _logger.LogAgentStatus("canceling", "Canceling current processing");
                _processingCts.Cancel();
                _processingCts.Dispose();
                _processingCts = null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error canceling processing: {ex.Message}");
        }
    }
    #endregion

    #region Error Handling
    /// <summary>
    /// Handles errors that occur during processing.
    /// </summary>
    /// <param name="ex">The exception that occurred.</param>
    /// <param name="cancellationToken">Token to cancel the operation if needed.</param>
    private async Task HandleErrorAsync(Exception ex, CancellationToken cancellationToken)
    {
        _logger.LogError($"Error occurred: {ex.Message}");

        var errorMessage = ex switch
        {
            HttpRequestException => "I'm having trouble connecting to my language processing system.",
            JsonException => "I received an invalid response format.",
            _ => "I encountered an unexpected error."
        };

        if (!_isMuted)
        {
            await _audioOutputModule.SpeakAsync(
                $"{errorMessage} Please try again in a moment.",
                cancellationToken);
        }
        else
        {
            EventBus.Instance.Publish(new SystemStateEvent
            {
                State = SystremState.Listening.ToString()
            });
        }
    }
    #endregion

    #region IDisposable Implementation
    /// <summary>
    /// Disposes the resources used by the Alita Agent.
    /// </summary>
    /// <param name="disposing">Indicates whether the method is called from Dispose.</param>
    private void Dispose(bool disposing)
    {
        if (_isDisposed) return;
        if (disposing)
        {
            _logger.LogAgentStatus("disposing", "Cleaning up Alita Agent resources");

            _transcriptionService.OnTranscriptionResult -= HandleTranscriptionResult;
            _transcriptionService.PartialTranscriptReceived -= HandlePartialTranscriptionResult;
            _transcriptionService.StopListening();

            CancelCurrentProcessing();
            _listeningCts?.Cancel();
            _listeningCts?.Dispose();
        }

        _isDisposed = true;
    }

    /// <summary>
    /// Disposes the Alita Agent.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Finalizes the Alita Agent.
    /// </summary>
    ~AlitaAgent()
    {
        Dispose(false);
    }
    #endregion

    /// <summary>
    /// Initializes the system prompt and loads session instructions.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation if needed</param>
    private async Task InitializeSystemPromptAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogMemory("store", "Loading system instructions");

            // Update System Instruction
            await _conversationStore.SaveMessageAsync(new Message
            {
                Role = "system",
                Content = _starkProtocols.SessionInstructions
            });

            _logger.LogMemory("ready", "System instructions loaded");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error during system prompt initialization: {ex.Message}");
            throw new InvalidOperationException("Failed to initialize system prompt", ex);
        }
    }

    /// <summary>
    /// Sets the mute state for voice output
    /// </summary>
    public void SetMute(bool mute)
    {
        _isMuted = mute;
        _logger.LogInformation($"Voice output {(_isMuted ? "muted" : "unmuted")}");
    }

    /// <summary>
    /// Gets the current mute state
    /// </summary>
    public bool IsMuted() => _isMuted;
}