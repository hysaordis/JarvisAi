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
    private const float MAX_16BIT_VALUE = 32768f; // Massimo valore per audio 16-bit
    private const int NORMALIZED_SCALE = 100; // Scala da 0-100
    #endregion

    #region Private Fields
    private readonly RealtimeTranscriber _transcriber;
    private readonly IJarvisLogger _logger;
    private readonly WaveInEvent _waveIn;
    private CancellationTokenSource _cts;
    private bool _isInitialized;
    private bool _isListening = false;
    private bool _isDisposed;
    private DateTime _lastStateChangeTime;
    private readonly SemaphoreSlim _reconnectSemaphore = new SemaphoreSlim(1, 1);
    private bool _isReconnecting;
    #endregion

    public event EventHandler<string> OnTranscriptionResult;
    public event EventHandler<Exception> OnError;
    public event EventHandler<string> PartialTranscriptReceived;

    public AssemblyAIRealtimeTranscriber(IJarvisLogger logger, IJarvisConfigManager configManager)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var apiKey = configManager.GetValue(API_KEY_CONFIG_NAME)
                     ?? throw new ArgumentNullException(API_KEY_CONFIG_NAME);

        _transcriber = new RealtimeTranscriber(new RealtimeTranscriberOptions
        {
            ApiKey = apiKey,
            SampleRate = SAMPLE_RATE
        });

        _waveIn = new WaveInEvent
        {
            WaveFormat = new WaveFormat(SAMPLE_RATE, BITS_PER_SAMPLE, CHANNELS),
            BufferMilliseconds = BUFFER_MILLISECONDS
        };

        _waveIn.DataAvailable += OnDataAvailable;
        _waveIn.RecordingStopped += OnRecordingStopped;

        _lastStateChangeTime = DateTime.UtcNow;

        _logger.LogDeviceStatus("initialized", $"Sample Rate: {SAMPLE_RATE}Hz, Channels: {CHANNELS}");
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (_isInitialized)
        {
            _logger.LogTranscriptionProgress("initialized", "Transcriber already initialized");
            return;
        }

        await ConnectWithRetryAsync(cancellationToken);
    }

    private async Task ConnectWithRetryAsync(CancellationToken cancellationToken)
    {
        int retryCount = 0;
        const int maxRetries = 3;
        const int retryDelayMs = 2000;

        while (retryCount < maxRetries)
        {
            try
            {
                _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                await SetupTranscriberEventsAsync();
                await _transcriber.ConnectAsync();
                _isInitialized = true;
                _logger.LogTranscriptionProgress("ready", "Connected to AssemblyAI Realtime API");
                return;
            }
            catch (Exception ex)
            {
                retryCount++;
                _logger.LogTranscriberError(COMPONENT_NAME, $"Connection attempt {retryCount} failed: {ex.Message}");

                if (retryCount == maxRetries)
                {
                    _logger.LogTranscriberError(COMPONENT_NAME, "Max retry attempts reached");
                    OnError?.Invoke(this, ex);
                    throw;
                }

                await Task.Delay(retryDelayMs, cancellationToken);
            }
        }
    }

    private async Task SetupTranscriberEventsAsync()
    {
        _transcriber.PartialTranscriptReceived.Subscribe(transcript =>
        {
            if (transcript.Text == "") return;
            PartialTranscriptReceived?.Invoke(this, transcript.Text);
        });

        _transcriber.FinalTranscriptReceived.Subscribe(transcript =>
        {
            if (string.IsNullOrEmpty(transcript.Text)) return;
            _logger.LogTranscriptionResult(transcript.Text);
            OnTranscriptionResult?.Invoke(this, transcript.Text);
        });

        _transcriber.Closed.Subscribe(async (ClosedEventArgs args) =>
        {
            _logger.LogTranscriberError(COMPONENT_NAME, "Connection closed unexpectedly");
            await HandleConnectionLostAsync();
        });
    }

    private async Task HandleConnectionLostAsync()
    {
        if (_isReconnecting) return;

        try
        {
            await _reconnectSemaphore.WaitAsync();
            _isReconnecting = true;

            _logger.LogTranscriptionProgress("reconnecting", "Attempting to reconnect to AssemblyAI");
            await _transcriber.ConnectAsync();
            _logger.LogTranscriptionProgress("reconnected", "Successfully reconnected to AssemblyAI");
        }
        catch (Exception ex)
        {
            _logger.LogTranscriberError(COMPONENT_NAME, $"Reconnection failed: {ex.Message}");
            OnError?.Invoke(this, ex);
        }
        finally
        {
            _isReconnecting = false;
            _reconnectSemaphore.Release();
        }
    }

    private async void OnDataAvailable(object sender, WaveInEventArgs e)
    {
        try
        {
            if (_cts.Token.IsCancellationRequested) return;

            if (_isListening)
            {
                var buffer = new byte[e.BytesRecorded];
                Array.Copy(e.Buffer, buffer, e.BytesRecorded);

                // Calcola e pubblica il livello audio normalizzato
                var normalizedLevel = CalculateNormalizedAudioLevel(buffer);
                EventBus.Instance.Publish(new AudioInputLevelEvent
                {
                    Level = normalizedLevel
                });

                try
                {
                    await _transcriber.SendAudioAsync(new Memory<byte>(buffer));
                }
                catch (Exception ex) when (ex.Message.Contains("Disconnected"))
                {
                    await HandleConnectionLostAsync();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogTranscriberError(COMPONENT_NAME, $"Error processing audio data: {ex.Message}");
            OnError?.Invoke(this, ex);
        }
    }

    private float CalculateNormalizedAudioLevel(byte[] buffer)
    {
        int bytesPerSample = BITS_PER_SAMPLE / 8;
        int sampleCount = buffer.Length / bytesPerSample;
        float maxSample = 0f;

        // Trova il valore di picco nel buffer audio
        for (int i = 0; i < sampleCount; i++)
        {
            short sample = BitConverter.ToInt16(buffer, i * bytesPerSample);
            float absoluteSample = Math.Abs(sample);
            maxSample = Math.Max(maxSample, absoluteSample);
        }

        // Normalizza il valore su una scala da 0 a NORMALIZED_SCALE
        float normalizedLevel = (maxSample / MAX_16BIT_VALUE) * NORMALIZED_SCALE;

        // Applica una leggera curva logaritmica per una migliore percezione
        normalizedLevel = (float)(Math.Log10(normalizedLevel + 1) / Math.Log10(NORMALIZED_SCALE + 1) * NORMALIZED_SCALE);

        return Math.Min(normalizedLevel, NORMALIZED_SCALE);
    }

    public void StartListening()
    {
        ThrowIfNotInitialized();
        ThrowIfDisposed();

        if (_isListening)
        {
            _logger.LogTranscriptionProgress("busy", "Already listening");
            return;
        }

        try
        {
            _waveIn.StartRecording();
            _isListening = true;
            _logger.LogTranscriptionProgress("listening", "Started realtime audio capture");
        }
        catch (Exception ex)
        {
            _logger.LogTranscriberError(COMPONENT_NAME, $"Failed to start listening: {ex.Message}");
            OnError?.Invoke(this, ex);
            throw;
        }
    }

    public void StopListening()
    {
        if (_isDisposed || !_isListening) return;

        try
        {
            _waveIn.StopRecording();
            _isListening = false;
            _logger.LogTranscriptionProgress("stopping", "Stopped realtime audio capture");
        }
        catch (Exception ex)
        {
            _logger.LogTranscriberError(COMPONENT_NAME, $"Error while stopping: {ex.Message}");
            OnError?.Invoke(this, ex);
        }
    }

    private void OnRecordingStopped(object sender, StoppedEventArgs e)
    {
        if (e.Exception != null)
        {
            _logger.LogTranscriberError(COMPONENT_NAME, $"Recording stopped due to error: {e.Exception.Message}");
            OnError?.Invoke(this, e.Exception);
        }
        else
        {
            _logger.LogTranscriptionProgress("completed", "Recording stopped successfully");
        }
    }

    private void ThrowIfNotInitialized()
    {
        if (!_isInitialized)
        {
            throw new InvalidOperationException("Realtime transcriber not initialized. Call InitializeAsync first.");
        }
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(AssemblyAIRealtimeTranscriber));
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed) return;

        try
        {
            StopListening();
            _reconnectSemaphore.Dispose();

            if (_transcriber != null)
            {
                await _transcriber.CloseAsync();
                await _transcriber.DisposeAsync();
            }

            _waveIn?.Dispose();
            _cts?.Dispose();

            _isDisposed = true;
            _logger.LogDeviceStatus("stopping", "Resources cleaned up successfully");
        }
        catch (Exception ex)
        {
            _logger.LogTranscriberError(COMPONENT_NAME, $"Error during disposal: {ex.Message}");
        }
    }
}