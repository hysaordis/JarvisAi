using System.Text;
using Jarvis.Ai.Common.Utils;
using Jarvis.Ai.Interfaces;
using NAudio.Wave;
using Whisper.net;

namespace Jarvis.Ai.Features.AudioProcessing;

public sealed class WhisperTranscriber : ITranscriber
{
    #region Constants
    private const int DEVICE_NUMBER = 1;
    private const int BUFFER_SECONDS = 5; // Increased for better context
    private const int SAMPLE_RATE = 16000;
    private const int BITS_PER_SAMPLE = 16;
    private const int CHANNELS = 1;
    private const int BUFFER_MILLISECONDS = 100; // Increased for more stable processing
    private const int MAX_QUEUE_SIZE = SAMPLE_RATE * BUFFER_SECONDS;
    private const float SILENCE_THRESHOLD = 0.01f;
    #endregion

    private readonly string _whisperModelPath;
    private readonly IJarvisLogger _logger;
    private readonly IJarvisConfigManager _configManager;
    private readonly StringBuilder _transcriptionBuilder;
    private readonly RingBuffer<float> _audioBuffer;
    private readonly SemaphoreSlim _processingLock;

    private WhisperFactory _whisperFactory;
    private WhisperProcessor _whisperProcessor;
    private WaveInEvent _waveIn;
    private CancellationTokenSource _cts;
    private bool _isInitialized;
    private bool _isDisposed;
    private DateTime _lastSilenceTime;
    private bool _isSpeaking;

    public event EventHandler<string> OnTranscriptionResult;
    public event EventHandler<Exception> OnError;
    public event EventHandler<string> PartialTranscriptReceived;

    public WhisperTranscriber(
        IJarvisLogger logger,
        IJarvisConfigManager configManager)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configManager = configManager ?? throw new ArgumentNullException(nameof(configManager));
        _whisperModelPath = _configManager.GetValue("WHISPER_MODEL_PATH")
                           ?? throw new ArgumentNullException("WHISPER_MODEL_PATH");

        _transcriptionBuilder = new StringBuilder();
        _audioBuffer = new RingBuffer<float>(MAX_QUEUE_SIZE);
        _processingLock = new SemaphoreSlim(1, 1);
        _lastSilenceTime = DateTime.UtcNow;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (_isInitialized)
        {
            _logger.LogTranscriberError("initialization", "Transcriber already initialized");
            return;
        }

        try
        {
            _logger.LogDeviceStatus("starting", "Initializing Whisper model");

            await Task.Run(() =>
            {
                _whisperFactory = WhisperFactory.FromPath(_whisperModelPath);

                _whisperProcessor = _whisperFactory.CreateBuilder()
                    .WithLanguage("en")
                    .WithProbabilities()
                    .WithNoContext()
                    .WithTemperature(0.0f)
                    .WithNoSpeechThreshold(0.06f)
                    .WithMaxSegmentLength(30)
                    .WithPrintTimestamps()
                    .WithDuration(TimeSpan.FromSeconds(BUFFER_SECONDS))
                    .WithGreedySamplingStrategy()
                    .ParentBuilder
                    .Build();

            }, cancellationToken);

            InitializeAudioDevice();
            _isInitialized = true;

            _logger.LogDeviceStatus("ready", "Whisper transcriber initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogTranscriberError("initialization", ex.Message);
            OnError?.Invoke(this, ex);
            throw;
        }
    }

    private void InitializeAudioDevice()
    {
        _waveIn = new WaveInEvent
        {
            WaveFormat = new WaveFormat(SAMPLE_RATE, BITS_PER_SAMPLE, CHANNELS),
            DeviceNumber = DEVICE_NUMBER,
            BufferMilliseconds = BUFFER_MILLISECONDS
        };

        _waveIn.DataAvailable += OnDataAvailable;
        _waveIn.RecordingStopped += OnRecordingStopped;
    }

    public void StartListening()
    {
        ThrowIfNotInitialized();
        ThrowIfDisposed();

        try
        {
            _cts = new CancellationTokenSource();
            _waveIn.StartRecording();

            Task.Run(async () =>
            {
                try
                {
                    await ProcessAudioAsync(_cts.Token);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogTranscriberError("processing", ex.Message);
                    OnError?.Invoke(this, ex);
                }
            }, _cts.Token);

            _logger.LogTranscriptionProgress("listening", "Started listening for audio input");
        }
        catch (Exception ex)
        {
            _logger.LogTranscriberError("device", ex.Message);
            OnError?.Invoke(this, ex);
            throw;
        }
    }

    private void OnDataAvailable(object sender, WaveInEventArgs e)
    {
        try
        {
            int bytesRecorded = e.BytesRecorded;
            int bytesPerSample = _waveIn.WaveFormat.BitsPerSample / 8;
            int sampleCount = bytesRecorded / bytesPerSample;
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                short sample = BitConverter.ToInt16(e.Buffer, i * bytesPerSample);
                samples[i] = sample / 32768f;
            }

            // Voice activity detection with RMS level
            float rms = (float)Math.Sqrt(samples.Select(s => s * s).Average());
            bool currentlySpeaking = rms > SILENCE_THRESHOLD;

            if (currentlySpeaking != _isSpeaking)
            {
                _logger.LogVoiceActivity(currentlySpeaking, rms);
                _isSpeaking = currentlySpeaking;

                if (!_isSpeaking)
                {
                    _lastSilenceTime = DateTime.UtcNow;
                }
            }

            _audioBuffer.Write(samples, 0, samples.Length);
        }
        catch (Exception ex)
        {
            _logger.LogTranscriberError("audio", ex.Message);
            OnError?.Invoke(this, ex);
        }
    }

    private async Task ProcessAudioAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await _processingLock.WaitAsync(cancellationToken);

                var samplesNeeded = SAMPLE_RATE * BUFFER_SECONDS;
                if (_audioBuffer.Count >= samplesNeeded)
                {
                    var audioData = new float[samplesNeeded];
                    _audioBuffer.Read(audioData, 0, samplesNeeded);

                    await ProcessSegmentAsync(audioData, cancellationToken);
                    _transcriptionBuilder.Clear(); // Clear after processing
                }
            }
            finally
            {
                _processingLock.Release();
            }

            await Task.Delay(BUFFER_MILLISECONDS, cancellationToken);
        }
    }

    private async Task ProcessSegmentAsync(float[] audioData, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogTranscriptionProgress("processing", "Processing audio segment");

            await foreach (var segment in _whisperProcessor.ProcessAsync(audioData, cancellationToken))
            {
                var text = segment.Text.Trim().RemoveTagsPreserveContent();
                if (string.IsNullOrEmpty(text))
                    continue;

                // Filter out low probability segments
                if (segment.Probability < 0.6f)
                {
                    _logger.LogTranscriptionResult(text, segment.Probability);
                    continue;
                }

                _transcriptionBuilder.AppendLine(text);

                var completeTranscription = _transcriptionBuilder.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(completeTranscription))
                {
                    _logger.LogTranscriptionResult(completeTranscription, segment.Probability);
                    OnTranscriptionResult?.Invoke(this, completeTranscription);
                }
            }

            _logger.LogTranscriptionProgress("completed");
        }
        catch (Exception ex)
        {
            _logger.LogTranscriberError("transcription", ex.Message);
            OnError?.Invoke(this, ex);
        }
    }

    public void StopListening()
    {
        if (_isDisposed) return;

        try
        {
            _cts?.Cancel();
            _waveIn?.StopRecording();
            _logger.LogDeviceStatus("stopping", "Recording stopped");
        }
        catch (Exception ex)
        {
            _logger.LogTranscriberError("device", ex.Message);
            OnError?.Invoke(this, ex);
        }
    }

    private void OnRecordingStopped(object sender, StoppedEventArgs e)
    {
        if (e.Exception != null)
        {
            _logger.LogTranscriberError("device", e.Exception.Message);
            OnError?.Invoke(this, e.Exception);
        }
    }


    private void ThrowIfNotInitialized()
    {
        if (!_isInitialized)
        {
            throw new InvalidOperationException("Transcriber not initialized. Call InitializeAsync first.");
        }
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(WhisperTranscriber));
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;

        try
        {
            StopListening();
            _waveIn?.Dispose();
            _whisperProcessor?.Dispose();
            _whisperFactory?.Dispose();
            _cts?.Dispose();
            _processingLock?.Dispose();

            _isDisposed = true;
            _logger.LogDeviceStatus("stopping", "Transcriber disposed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogTranscriberError("disposal", ex.Message);
        }
    }
}

public class RingBuffer<T>
{
    private readonly T[] _buffer;
    private int _writeIndex;
    private int _readIndex;
    private int _count;
    private readonly object _lock = new object();

    public RingBuffer(int capacity)
    {
        _buffer = new T[capacity];
        _writeIndex = 0;
        _readIndex = 0;
        _count = 0;
    }

    public int Count
    {
        get { lock (_lock) { return _count; } }
    }

    public void Write(T[] data, int offset, int count)
    {
        lock (_lock)
        {
            for (int i = 0; i < count; i++)
            {
                _buffer[_writeIndex] = data[offset + i];
                _writeIndex = (_writeIndex + 1) % _buffer.Length;

                if (_count == _buffer.Length)
                {
                    _readIndex = (_readIndex + 1) % _buffer.Length; // Sovrascrive i dati più vecchi
                }
                else
                {
                    _count++;
                }
            }
        }
    }

    public int Read(T[] data, int offset, int count)
    {
        lock (_lock)
        {
            int readCount = Math.Min(count, _count);
            for (int i = 0; i < readCount; i++)
            {
                data[offset + i] = _buffer[_readIndex];
                _readIndex = (_readIndex + 1) % _buffer.Length;
            }
            _count -= readCount;
            return readCount;
        }
    }
}
