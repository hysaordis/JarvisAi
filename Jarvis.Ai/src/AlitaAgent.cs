using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks.Dataflow;
using Jarvis.Ai.Common.Settings;
using Jarvis.Ai.Interfaces;
using Jarvis.Ai.LLM;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Jarvis.Ai;

public class ResponseMessage
{
    [JsonProperty("role")]
    public string Role { get; set; }

    [JsonProperty("content")]
    public string Content { get; set; }

    [JsonProperty("tool_calls", NullValueHandling = NullValueHandling.Ignore)]
    public List<OllamaTools> ToolCalls { get; set; }
}

public class OllamaTools
{
    [JsonProperty("type")]
    public string Type { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("parameters")]
    public Dictionary<string, object> Parameters { get; set; }
}

public class AlitaAgent : IJarvis, IDisposable
{
    #region Private Fields
    private readonly ILlmClient _llmClient;
    private readonly ILogger<AlitaAgent> _logger;
    private readonly IModuleRegistry _moduleRegistry;
    private readonly ITranscriber _transcriptionService;
    private readonly IAudioOutputModule _audioOutputModule;
    private readonly List<Message> _conversationHistory;
    private readonly BufferBlock<string> _transcriptionBuffer;
    private readonly ConcurrentDictionary<string, byte> _processedTranscriptions = new();
    private readonly StarkProtocols _starkProtocols;
    private bool _isDisposed;
    private CancellationTokenSource _listeningCts;
    private volatile bool _isProcessing;
    #endregion

    #region Constructor
    public AlitaAgent(
        ILogger<AlitaAgent> logger,
        IModuleRegistry moduleRegistry,
        ITranscriber transcriptionService,
        IAudioOutputModule audioOutputModule,
        ILlmClient llmClient,
        StarkProtocols starkProtocols)
    {
        _logger = logger;
        _moduleRegistry = moduleRegistry;
        _transcriptionService = transcriptionService;
        _audioOutputModule = audioOutputModule;
        _llmClient = llmClient;
        _starkProtocols = starkProtocols;

        _conversationHistory = new List<Message>();
        _transcriptionBuffer = new BufferBlock<string>(new DataflowBlockOptions
        {
            BoundedCapacity = 100
        });

        _transcriptionService.OnTranscriptionResult += HandleTranscriptionResult;
    }
    #endregion

    #region IJarvis Implementation
    public async Task InitializeAsync(string[]? initialCommands, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Initializing OllamaLlmAgent");

        try
        {
            await _transcriptionService.InitializeAsync(cancellationToken);
            await InitializeSystemPromptAsync(cancellationToken);
            await StartListeningAsync(cancellationToken);

            if (initialCommands is { Length: > 0 })
            {
                _logger.LogInformation($"Processing {initialCommands.Length} initial commands");
                await ExecuteCommandsAsync(initialCommands, cancellationToken);
            }
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
        _logger.LogInformation("Initializing system prompt for Ollama");

        try
        {
            // Clear previous conversation history
            _conversationHistory.Clear();

            // Add system instructions
            _conversationHistory.Add(new Message
            {
                Role = "system",
                Content = _starkProtocols.SessionInstructions
            });

            //// Request an introduction from the AI
            //_conversationHistory.Add(new Message
            //{
            //    Role = "user",
            //    Content = "Please introduce yourself briefly and let me know you're ready to help."
            //});

            //// Get AI's introduction
            //var introductionResponse = await _llmClient.SendCommandToLlmAsync(
            //    _conversationHistory,
            //    cancellationToken
            //);

            //// Add the response to conversation history
            //_conversationHistory.Add(introductionResponse);

            //// Speak the introduction
            //if (!string.IsNullOrEmpty(introductionResponse.Content))
            //{
            //    _logger.LogInformation($"Speaking introduction: {introductionResponse.Content}");
            //    await _audioOutputModule.SpeakAsync(introductionResponse.Content, cancellationToken);
            //}
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error during system prompt initialization: {ex.Message}");
            throw new InvalidOperationException("Failed to initialize system prompt", ex);
        }
    }

    public Task ProcessAudioInputAsync(byte[] audioData, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public async Task<string> ListenForResponseAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (_isProcessing) return string.Empty;

            _logger.LogInformation("Waiting for transcription...");
            var transcription = await _transcriptionBuffer.ReceiveAsync(cancellationToken);

            if (string.IsNullOrEmpty(transcription))
                return transcription;

            // Se la trascrizione è già stata processata, saltiamo
            if (_processedTranscriptions.ContainsKey(transcription))
            {
                _logger.LogInformation($"Skipping already processed transcription: {transcription}");
                return string.Empty;
            }

            _logger.LogInformation($"Processing new transcription: {transcription}");

            try
            {
                await ProcessCommandAsync(transcription, cancellationToken);

                // Marca la trascrizione come processata dopo il completamento
                _processedTranscriptions.TryAdd(transcription, 0);

                // Opzionale: pulisci la cronologia se diventa troppo grande
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
            _logger.LogError(ex, "Error in ListenForResponseAsync");
            return string.Empty;
        }
    }

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

    public Task ShutdownAsync()
    {
        _logger.LogInformation("Shutting down OllamaLlmAgent");
        _transcriptionService.StopListening();
        _listeningCts?.Cancel();
        return Task.CompletedTask;
    }
    #endregion

    #region Audio Processing
    private async Task StartListeningAsync(CancellationToken cancellationToken)
    {
        _listeningCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _transcriptionService.StartListening();
        _logger.LogInformation("Started listening");
        // await _audioOutputModule.SpeakAsync("Ready for commands", cancellationToken);
    }

    private void HandleTranscriptionResult(object sender, string transcription)
    {
        if (string.IsNullOrEmpty(transcription) || _isProcessing) return;

        // Verifica se abbiamo già processato questa trascrizione
        if (!_processedTranscriptions.ContainsKey(transcription))
        {
            _logger.LogInformation($"New transcription received: {transcription}");
            _transcriptionBuffer.Post(transcription);
        }
        else
        {
            _logger.LogInformation($"Transcription already processed, ignoring: {transcription}");
        }
    }
    #endregion

    #region Command Processing
    private async Task ProcessCommandAsync(string command, CancellationToken cancellationToken)
    {
        if (_isProcessing)
        {
            _logger.LogInformation("Already processing a command, skipping...");
            return;
        }

        try
        {
            _isProcessing = true;
            _logger.LogInformation($"Processing command: {command}");
            _conversationHistory.Add(new Message { Role = "user", Content = command });

            while (!cancellationToken.IsCancellationRequested)
            {
                var response = await _llmClient.SendCommandToLlmAsync(_conversationHistory, cancellationToken);

                if (response.ToolCalls == null || !string.IsNullOrEmpty(response.Content))
                {
                    _logger.LogInformation($"Received direct response: {response.Content}");
                    await _audioOutputModule.SpeakAsync(response.Content, cancellationToken);
                    break;
                }

                foreach (var toolCall in response.ToolCalls)
                {
                    await ExecuteToolCallAsync(toolCall, cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex, cancellationToken);
        }
        finally
        {
            _isProcessing = false;
        }
    }

    private async Task ExecuteToolCallAsync(FunctionCall functionCall, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation($"Executing tool call: {functionCall.Function.Name}");

            var funcArgs = JsonConvert.SerializeObject(functionCall.Function.Arguments);
            var args = JsonConvert.DeserializeObject<Dictionary<string, object>>(funcArgs);
            var result = await _moduleRegistry.ExecuteCommand(functionCall.Function.Name, args);
            var functionResponse = JsonConvert.SerializeObject(result);

            _logger.LogInformation($"Tool call result: {functionResponse}");
            _conversationHistory.Add(new Message
            {
                Role = "tool",
                Content = functionResponse,
                ToolCallId = functionCall.Id
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error executing tool call {functionCall.Function.Name}: {ex.Message}");
            _conversationHistory.Add(new Message
            {
                Role = "tool",
                Content = $"Error: {ex.Message}",
                ToolCallId = functionCall.Id
            });
        }
    }
    #endregion

    #region Error Handling
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
    protected virtual void Dispose(bool disposing)
    {
        if (_isDisposed) return;
        if (disposing)
        {
            _logger.LogInformation("Disposing AlitaAgent resources");

            _transcriptionService.OnTranscriptionResult -= HandleTranscriptionResult;
            _transcriptionService.StopListening();

            _listeningCts?.Cancel();
            _listeningCts?.Dispose();

            _conversationHistory.Clear();
            _transcriptionBuffer.Complete();
        }

        _isDisposed = true;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~AlitaAgent()
    {
        Dispose(false);
    }
    #endregion
}