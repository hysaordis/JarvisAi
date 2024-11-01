using NAudio.Wave;
using AssemblyAI.Realtime;
using Jarvis.Ai.Interfaces;

namespace Jarvis.Ai.Features.AudioProcessing;

public sealed class AssemblyAIRealtimeTranscriber : ITranscriber, IAsyncDisposable
{
    #region Constants
    private const int SAMPLE_RATE = 16000;
    private const int BITS_PER_SAMPLE = 16;
    private const int CHANNELS = 1;
    private const int BUFFER_MILLISECONDS = 100;
    private const float SILENCE_THRESHOLD = 0.01f;
    private const string COMPONENT_NAME = "AssemblyAI Realtime";
    private const string API_KEY_CONFIG_NAME = "ASSEMBLYAI_API_KEY";
    #endregion

    #region Private Fields
    private readonly RealtimeTranscriber _transcriber;
    private readonly IJarvisLogger _logger;
    private readonly WaveInEvent _waveIn;
    private CancellationTokenSource _cts;
    private bool _isInitialized;
    private bool _isListening;
    private bool _isDisposed;
    private DateTime _lastStateChangeTime;
    private bool _isSpeaking;
    private readonly WakeWordDetector _wakeWordDetector;
    private bool _isActivated;
    private readonly float[] _tempBuffer;
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

        _wakeWordDetector = new WakeWordDetector(logger);
        _wakeWordDetector.WakeWordDetected += OnWakeWordDetected;
        _tempBuffer = new float[SAMPLE_RATE]; // 1 second buffer
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (_isInitialized)
        {
            _logger.LogTranscriptionProgress("initialized", "Transcriber already initialized");
            return;
        }

        try
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            _transcriber.PartialTranscriptReceived.Subscribe(transcript =>
            {
                if (transcript.Text == "") return;
                PartialTranscriptReceived?.Invoke(this, transcript.Text);
            });

            _transcriber.FinalTranscriptReceived.Subscribe(transcript =>
            {
                if (string.IsNullOrEmpty(transcript.Text))
                    return;

                _logger.LogTranscriptionResult(transcript.Text);
                OnTranscriptionResult?.Invoke(this, transcript.Text);
            });

            await _transcriber.ConnectAsync();
            _isInitialized = true;
            _logger.LogTranscriptionProgress("ready", "Connected to AssemblyAI Realtime API");
        }
        catch (Exception ex)
        {
            _logger.LogTranscriberError(COMPONENT_NAME, $"Initialization failed: {ex.Message}");
            OnError?.Invoke(this, ex);
            throw;
        }
    }

    private async void OnDataAvailable(object sender, WaveInEventArgs e)
    {
        try
        {
            if (!_isListening || _cts.Token.IsCancellationRequested) return;

            // Convert bytes to float array for wake word detection
            int bytesPerSample = _waveIn.WaveFormat.BitsPerSample / 8;
            int sampleCount = e.BytesRecorded / bytesPerSample;
            
            for (int i = 0; i < sampleCount; i++)
            {
                short sample = BitConverter.ToInt16(e.Buffer, i * bytesPerSample);
                _tempBuffer[i] = sample / 32768f;
            }

            // Always process audio for wake word detection
            await _wakeWordDetector.ProcessAudioSegment(_tempBuffer);

            // Only send audio to AssemblyAI if activated
            if (_isActivated)
            {
                _logger.LogTranscriptionProgress("transcribing", "Sending audio to AssemblyAI");
                var buffer = new Memory<byte>(e.Buffer, 0, e.BytesRecorded);
                await _transcriber.SendAudioAsync(buffer).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogTranscriberError(COMPONENT_NAME, $"Error processing audio data: {ex.Message}");
            OnError?.Invoke(this, ex);
        }
    }

    private float CalculateRMS(byte[] buffer, int bytesRecorded)
    {
        int bytesPerSample = _waveIn.WaveFormat.BitsPerSample / 8;
        int sampleCount = bytesRecorded / bytesPerSample;
        float sumSquares = 0.0f;

        for (int i = 0; i < sampleCount; i++)
        {
            short sample = BitConverter.ToInt16(buffer, i * bytesPerSample);
            float normalizedSample = sample / 32768f;
            sumSquares += normalizedSample * normalizedSample;
        }

        return (float)Math.Sqrt(sumSquares / sampleCount);
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

    private void OnWakeWordDetected(object sender, bool detected)
    {
        if (!_isActivated && detected)
        {
            _isActivated = true;
            _logger.LogTranscriptionProgress("activated", "Wake word detected, starting transcription");
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

            if (_transcriber != null)
            {
                await _transcriber.CloseAsync();
                await _transcriber.DisposeAsync();
            }

            _waveIn?.Dispose();
            _cts?.Dispose();
            _wakeWordDetector.Dispose();

            _isDisposed = true;
            _logger.LogDeviceStatus("stopping", "Resources cleaned up successfully");
        }
        catch (Exception ex)
        {
            _logger.LogTranscriberError(COMPONENT_NAME, $"Error during disposal: {ex.Message}");
        }
    }
}