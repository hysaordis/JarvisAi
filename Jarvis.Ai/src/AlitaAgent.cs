using System.Collections.Concurrent;
using System.Threading.Tasks.Dataflow;
using Jarvis.Ai.Common.Settings;
using Jarvis.Ai.Interfaces;
using Jarvis.Ai.LLM;
using Jarvis.Ai.Persistence;
using Newtonsoft.Json;

namespace Jarvis.Ai;

/// <summary>
/// Represents the main agent responsible for handling transcription, processing commands, and interacting with various modules.
/// </summary>
public class AlitaAgent : IJarvis, IDisposable
{
    #region Private Fields
    private readonly ILlmClient _llmClient;
    private readonly IJarvisLogger _logger;
    private readonly IModuleRegistry _moduleRegistry;
    private readonly ITranscriber _transcriptionService;
    private readonly IAudioOutputModule _audioOutputModule;
    private readonly BufferBlock<string> _transcriptionBuffer;
    private readonly ConcurrentDictionary<string, byte> _processedTranscriptions = new();
    private readonly StarkProtocols _starkProtocols;
    private bool _isDisposed;
    private CancellationTokenSource _listeningCts;
    private CancellationTokenSource? _processingCts;
    private volatile bool _isProcessing;
    private readonly IConversationStore _conversationStore;

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
        _transcriptionBuffer = new BufferBlock<string>(new DataflowBlockOptions
        {
            BoundedCapacity = 100
        });

        _transcriptionService.OnTranscriptionResult += HandleTranscriptionResult;
        _transcriptionService.PartialTranscriptReceived += HandlePartialTranscriptionResult;
        _conversationStore = conversationStore;
    }

    #endregion

    #region IJarvis Implementation
    /// <summary>
    /// Initializes the Alita Agent asynchronously.
    /// </summary>
    /// <param name="initialCommands">Optional initial commands to execute after initialization.</param>
    /// <param name="cancellationToken">Token to cancel the operation if needed.</param>
    public async Task InitializeAsync(string[]? initialCommands, CancellationToken cancellationToken)
    {

        try
        {
            _logger.LogAgentStatus("initializing", "Starting Alita Agent system");

            await _transcriptionService.InitializeAsync(cancellationToken);
            await InitializeSystemPromptAsync(cancellationToken);
            await StartListeningAsync(cancellationToken);

            if (initialCommands is { Length: > 0 })
            {
                _logger.LogInformation($"Processing {initialCommands.Length} initial commands");
                await ExecuteCommandsAsync(initialCommands, cancellationToken);
            }

            _logger.LogAgentStatus("ready", "Alita Agent initialized and ready");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Initialization failed: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Initializes the system prompt and triggers an introduction from the AI.
    /// Includes clearing conversation history, setting up system instructions,
    /// and performing an initial greeting.
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

            var messages = await _conversationStore.GetAllMessagesAsync();

            // Request an introduction from the AI
            await _conversationStore.SaveMessageAsync(new Message
            {
                Role = "user",
                Content = "Greet the user in a friendly manner. If there are any incomplete tasks in the message list, " +
                "briefly describe the task and ask if they'd like to proceed with its completion."
            });

            // Get updated messages including the greeting request
            messages = await _conversationStore.GetAllMessagesAsync();

            // Get AI's introduction with the complete message history
            var introductionResponse = await _llmClient.SendCommandToLlmAsync(
                messages,
                cancellationToken
            );

            // Speak the introduction
            if (!string.IsNullOrEmpty(introductionResponse.Content))
            {
                _logger.LogInformation($"Speaking introduction: {introductionResponse.Content}");
                await _audioOutputModule.SpeakAsync(introductionResponse.Content, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error during system prompt initialization: {ex.Message}");
            throw new InvalidOperationException("Failed to initialize system prompt", ex);
        }
    }

    /// <summary>
    /// Processes the audio input asynchronously.
    /// </summary>
    /// <param name="audioData">The audio data to process.</param>
    /// <param name="cancellationToken">Token to cancel the operation if needed.</param>
    public Task ProcessAudioInputAsync(byte[] audioData, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Listens for a response asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation if needed.</param>
    /// <returns>The transcription of the response.</returns>
    public async Task<string> ListenForResponseAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (_isProcessing) return string.Empty;

            _logger.LogAgentStatus("listening", "Waiting for user input");

            var transcription = await _transcriptionBuffer.ReceiveAsync(cancellationToken);

            if (string.IsNullOrEmpty(transcription))
                return transcription;

            // Se la trascrizione è già stata processata, saltiamo
            if (_processedTranscriptions.ContainsKey(transcription))
            {
                _logger.LogThinking("Skipping duplicate transcription", transcription);
                return string.Empty;
            }

            try
            {
                await ProcessCommandAsync(transcription, cancellationToken);

                _processedTranscriptions.TryAdd(transcription, 0);

                if (_processedTranscriptions.Count > 1000)
                {
                    var oldestTranscriptions = _processedTranscriptions.Keys.Take(_processedTranscriptions.Count - 500);
                    foreach (var old in oldestTranscriptions)
                    {
                        _processedTranscriptions.TryRemove(old, out _);
                    }
                }

                return transcription;
            }
            finally
            {
                _isProcessing = false;
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Listening operation cancelled");
            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError("Error in ListenForResponseAsync", ex);
            return string.Empty;
        }
    }

    /// <summary>
    /// Executes the given commands asynchronously.
    /// </summary>
    /// <param name="commands">The commands to execute.</param>
    /// <param name="cancellationToken">Token to cancel the operation if needed.</param>
    public async Task ExecuteCommandsAsync(string[] commands, CancellationToken cancellationToken)
    {
        foreach (var command in commands)
        {
            if (_isProcessing)
            {
                _logger.LogInformation($"Skipping command {command} as another command is being processed");
                continue;
            }

            _logger.LogInformation($"Executing command: {command}");
            await ProcessCommandAsync(command, cancellationToken);
        }
    }

    /// <summary>
    /// Shuts down the Alita Agent asynchronously.
    /// </summary>
    public Task ShutdownAsync()
    {
        _logger.LogAgentStatus("shutdown", "Initiating shutdown sequence");
        _transcriptionService.StopListening();
        _listeningCts?.Cancel();
        return Task.CompletedTask;
    }
    #endregion

    #region Audio Processing
    /// <summary>
    /// Starts listening for audio input asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation if needed.</param>
    private async Task StartListeningAsync(CancellationToken cancellationToken)
    {
        _listeningCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _transcriptionService.StartListening();
        _logger.LogInformation("Started listening");
    }

    /// <summary>
    /// Handles partial transcription results.
    /// </summary>
    /// <param name="sender">The sender of the event.</param>
    /// <param name="e">The partial transcription result.</param>
    private void HandlePartialTranscriptionResult(object? sender, string e)
    {
        CancelCurrentProcessing();
    }

    /// <summary>
    /// Handles transcription results.
    /// </summary>
    /// <param name="sender">The sender of the event.</param>
    /// <param name="transcription">The transcription result.</param>
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
            _transcriptionBuffer.Post(transcription);
        }
        else
        {
            _logger.LogThinking("Skipping duplicate transcription", transcription);
        }
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
                    await _audioOutputModule.SpeakAsync(response.Content, processingToken);
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

        await _audioOutputModule.SpeakAsync(
            $"{errorMessage} Please try again in a moment.",
            cancellationToken);
    }
    #endregion

    #region IDisposable Implementation
    /// <summary>
    /// Disposes the resources used by the Alita Agent.
    /// </summary>
    /// <param name="disposing">Indicates whether the method is called from Dispose.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_isDisposed) return;
        if (disposing)
        {
            _logger.LogAgentStatus("disposing", "Cleaning up Alita Agent resources");

            _transcriptionService.OnTranscriptionResult -= HandleTranscriptionResult;
            _transcriptionService.StopListening();

            CancelCurrentProcessing();
            _listeningCts?.Cancel();
            _listeningCts?.Dispose();

            _transcriptionBuffer.Complete();
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
}