using NAudio.Wave;
using AssemblyAI.Realtime;
using Jarvis.Ai.Interfaces;
using Jarvis.Ai.Core.Events;

namespace Jarvis.Ai.Features.AudioProcessing;

public sealed class AssemblyAIRealtimeTranscriber : ITranscriber, IAsyncDisposable
{
    #region Constants
    private const int SAMPLE_RATE = 16000;
    private const int BITS_PER_SAMPLE = 16;
    private const int CHANNELS = 1;
    private const int BUFFER_MILLISECONDS = 100;
    private const string COMPONENT_NAME = "AssemblyAI Realtime";
    private const string API_KEY_CONFIG_NAME = "ASSEMBLYAI_API_KEY";
    #endregion

    private readonly RealtimeTranscriber _transcriber;
    private readonly IJarvisLogger _logger;
    private readonly WaveInEvent _waveIn;
    private CancellationTokenSource _cts;
    private bool _isInitialized;
    private bool _isListening;
    private bool _isDisposed;

    public event EventHandler<string> OnTranscriptionResult;
    public event EventHandler<Exception> OnError;
    public event EventHandler<string> PartialTranscriptReceived;

    public AssemblyAIRealtimeTranscriber(IJarvisLogger logger, IJarvisConfigManager configManager)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        var apiKey = configManager.GetValue(API_KEY_CONFIG_NAME) 
                    ?? throw new ArgumentNullException(API_KEY_CONFIG_NAME);
        var deviceNumber = int.Parse(configManager.GetValue("AUDIO_DEVICE_NUMBER") ?? "-1");

        _transcriber = new RealtimeTranscriber(new RealtimeTranscriberOptions
        {
            ApiKey = apiKey,
            SampleRate = SAMPLE_RATE
        });

        _waveIn = new WaveInEvent
        {
            WaveFormat = new WaveFormat(SAMPLE_RATE, BITS_PER_SAMPLE, CHANNELS),
            BufferMilliseconds = BUFFER_MILLISECONDS,
            DeviceNumber = deviceNumber
        };

        _waveIn.DataAvailable += OnDataAvailable;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (_isInitialized) return;

        try
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            SetupTranscriberEvents();
            await _transcriber.ConnectAsync();
            _isInitialized = true;
            _logger.LogTranscriptionProgress("ready", "Connected to AssemblyAI");
        }
        catch (Exception ex)
        {
            _logger.LogTranscriberError(COMPONENT_NAME, $"Initialization failed: {ex.Message}");
            OnError?.Invoke(this, ex);
            throw;
        }
    }

    private void SetupTranscriberEvents()
    {
        _transcriber.PartialTranscriptReceived.Subscribe(transcript =>
        {
            if (!string.IsNullOrEmpty(transcript.Text))
                PartialTranscriptReceived?.Invoke(this, transcript.Text);
        });

        _transcriber.FinalTranscriptReceived.Subscribe(transcript =>
        {
            if (!string.IsNullOrEmpty(transcript.Text))
            {
                _logger.LogTranscriptionResult(transcript.Text);
                OnTranscriptionResult?.Invoke(this, transcript.Text);
            }
        });
    }

    private async void OnDataAvailable(object sender, WaveInEventArgs e)
    {
        try
        {
            if (_isListening && !_cts.Token.IsCancellationRequested)
            {
                await _transcriber.SendAudioAsync(new Memory<byte>(e.Buffer, 0, e.BytesRecorded));
            }
        }
        catch (Exception ex)
        {
            _logger.LogTranscriberError(COMPONENT_NAME, $"Error sending audio: {ex.Message}");
            OnError?.Invoke(this, ex);
        }
    }

    public void StartListening()
    {
        if (!_isInitialized)
            throw new InvalidOperationException("Transcriber not initialized");
        if (_isListening) return;

        _waveIn.StartRecording();
        _isListening = true;
    }

    public void StopListening()
    {
        if (!_isListening) return;
        _waveIn.StopRecording();
        _isListening = false;
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed) return;

        try
        {
            StopListening();
            if (_transcriber != null)
            {
                await _transcriber.CloseAsync();
                await _transcriber.DisposeAsync();
            }
            _waveIn?.Dispose();
            _cts?.Dispose();
            _isDisposed = true;
        }
        catch (Exception ex)
        {
            _logger.LogTranscriberError(COMPONENT_NAME, $"Error during disposal: {ex.Message}");
        }
    }
}