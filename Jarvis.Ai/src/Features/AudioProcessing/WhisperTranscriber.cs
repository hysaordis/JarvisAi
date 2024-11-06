using System.Text;
using Jarvis.Ai.Common.Utils;
using Jarvis.Ai.Core.Events;
using Jarvis.Ai.Interfaces;
using NAudio.Wave;
using Whisper.net;

namespace Jarvis.Ai.Features.AudioProcessing;

public sealed class WhisperTranscriber : ITranscriber, IDisposable
{
    #region Constants
    private const int BUFFER_SECONDS = 5; // Increased for better context
    private const int SAMPLE_RATE = 16000;
    private const int BITS_PER_SAMPLE = 16;
    private const int CHANNELS = 1;
    private const int BUFFER_MILLISECONDS = 100; // Increased for more stable processing
    private const int MAX_QUEUE_SIZE = SAMPLE_RATE * BUFFER_SECONDS;
    private const float SILENCE_THRESHOLD = 0.01f;
    private const string MODEL_URL = "https://huggingface.co/sandrohanea/whisper.net/resolve/main/classic/ggml-base.bin";
    private const string MODEL_FILENAME = "ggml-base.bin";
    private const string DEFAULT_MODEL_PATH = "Jarvis/Models/Whisper"; // This is now a relative path
    #endregion

    private readonly string _whisperModelPath;
    private readonly IJarvisLogger _logger;
    private readonly IJarvisConfigManager _configManager;
    private readonly StringBuilder _transcriptionBuilder;
    private readonly RingBuffer<float> _audioBuffer;
    private readonly SemaphoreSlim _processingLock;
    private readonly int _deviceNumber;
    private readonly string _language;

    private WhisperFactory _whisperFactory;
    private WhisperProcessor _whisperProcessor;
    private WaveInEvent _waveIn;
    private CancellationTokenSource _cts;
    private bool _isInitialized;
    private bool _isDisposed;
    private DateTime _lastSilenceTime;
    private bool _isSpeaking;
    private bool _isListening = false;

    private bool isLongTextMode = false;
    private StringBuilder longTextBuilder = new StringBuilder();

    // Add lists of commands to start and stop long text mode
    private readonly List<string> longTextStartCommands = new List<string> { "whisper", "hey", "ehi", "jarvis", "alita" };
    private readonly List<string> longTextEndCommands = new List<string> { "procedi", "stop", "procedere", "grazie", "thank you" };

    public event EventHandler<string> OnTranscriptionResult;
    public event EventHandler<Exception> OnError;
    public event EventHandler<string> PartialTranscriptReceived;

    public WhisperTranscriber(
        IJarvisLogger logger,
        IJarvisConfigManager configManager)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configManager = configManager ?? throw new ArgumentNullException(nameof(configManager));

        // Get model path from config or use default AppData path
        string configuredPath = _configManager.GetValue("WHISPER_MODEL_PATH");
        if (!string.IsNullOrEmpty(configuredPath))
        {
            _whisperModelPath = configuredPath;
        }
        else
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string modelDir = Path.Combine(appData, DEFAULT_MODEL_PATH);
            _whisperModelPath = Path.Combine(modelDir, MODEL_FILENAME);
        }

        // Get device number from config
        var deviceNumberStr = _configManager.GetValue("AUDIO_DEVICE_NUMBER") ?? "-1";
        _deviceNumber = int.Parse(deviceNumberStr);

        // Get language from config or default to "en"
        _language = _configManager.GetValue("WHISPER_LANGUAGE") ?? "en";

        _transcriptionBuilder = new StringBuilder();
        _audioBuffer = new RingBuffer<float>(MAX_QUEUE_SIZE);
        _processingLock = new SemaphoreSlim(1, 1);
        _lastSilenceTime = DateTime.UtcNow;
    }

    private async Task EnsureModelDownloaded()
    {
        if (string.IsNullOrEmpty(_whisperModelPath))
        {
            var message = "Whisper model path is not configured.";
            _logger.LogTranscriberError("configuration", message);
            throw new InvalidOperationException(message);
        }

        if (!File.Exists(_whisperModelPath))
        {
            try
            {
                _logger.LogDeviceStatus("downloading", "Downloading Whisper model for transcription...");

                string modelDirectory = Path.GetDirectoryName(_whisperModelPath);
                if (!string.IsNullOrEmpty(modelDirectory))
                {
                    Directory.CreateDirectory(modelDirectory);
                }

                using (var httpClient = new HttpClient())
                using (var response = await httpClient.GetAsync(MODEL_URL))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        throw new HttpRequestException($"Failed to download model: {response.StatusCode}");
                    }

                    using (var fs = new FileStream(_whisperModelPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        await response.Content.CopyToAsync(fs);
                    }
                }

                _logger.LogDeviceStatus("downloaded", "Whisper model downloaded successfully.");
            }
            catch (Exception ex)
            {
                var message = $"Error downloading Whisper model: {ex.Message}";
                _logger.LogTranscriberError("download", message);
                throw new Exception(message, ex);
            }
        }
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (!_isInitialized)
        {
            await EnsureModelDownloaded();
        }

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
                    .WithLanguage(_language)
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
            DeviceNumber = _deviceNumber,
            BufferMilliseconds = BUFFER_MILLISECONDS
        };

        _waveIn.DataAvailable += OnDataAvailable;
        _waveIn.RecordingStopped += OnRecordingStopped;
    }

    public void StartListening()
    {
        ThrowIfNotInitialized();
        ThrowIfDisposed();

        if (_isListening)
        {
            _logger.LogTranscriberError("start", "Transcriber is already listening");
            return;
        }

        try
        {
            _cts = new CancellationTokenSource();
            _waveIn.StartRecording();
            _isListening = true;

            Task.Run(async () =>
            {
                try
                {
                    await ProcessAudioAsync(_cts.Token);
                }
                catch (OperationCanceledException)
                {
                    // Gracefully handle cancellation
                }
                catch (Exception ex)
                {
                    _logger.LogTranscriberError("processing", ex.Message);
                    OnError?.Invoke(this, ex);
                }
            }, _cts.Token);

            _logger.LogTranscriptionProgress("listening", "Started listening for audio input");
        }
        catch (Exception ex)
        {
            var message = $"Failed to start listening: {ex.Message}";
            _logger.LogTranscriberError("device", message);
            OnError?.Invoke(this, new Exception(message, ex));
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
            if (_audioBuffer.Count >= SAMPLE_RATE * BUFFER_SECONDS)
            {
                try
                {
                    await _processingLock.WaitAsync(cancellationToken);

                    var audioData = new float[SAMPLE_RATE * BUFFER_SECONDS];
                    _audioBuffer.Read(audioData, 0, audioData.Length);

                    await ProcessSegmentAsync(audioData, cancellationToken);
                }
                finally
                {
                    _processingLock.Release();
                }
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

                // Convert text to lowercase for case-insensitive matching
                var lowerText = text.ToLowerInvariant();

                // Check for start commands
                if (longTextStartCommands.Any(cmd => lowerText.Contains(cmd)))
                {
                    isLongTextMode = true;
                    _logger.LogTranscriptionProgress("mode", "Entered long text mode");
                    InvokeLongTextModeEvent(true); // Invoke event for entering long text mode
                    continue;
                }

                // Check for end commands
                if (isLongTextMode && longTextEndCommands.Any(cmd => lowerText.Contains(cmd)))
                {
                    isLongTextMode = false;
                    var completeTranscription = longTextBuilder.ToString().Trim();
                    if (!string.IsNullOrWhiteSpace(completeTranscription))
                    {
                        _logger.LogTranscriptionResult(completeTranscription, segment.Probability);
                        OnTranscriptionResult?.Invoke(this, completeTranscription);
                    }
                    longTextBuilder.Clear();
                    _logger.LogTranscriptionProgress("mode", "Exited long text mode");
                    InvokeLongTextModeEvent(false); // Invoke event for exiting long text mode
                    continue;
                }

                // Handle transcription based on mode
                if (isLongTextMode)
                {
                    longTextBuilder.AppendLine(text);
                }
                else
                {
                    // Existing behavior
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
                    _transcriptionBuilder.Clear();
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

    private void InvokeLongTextModeEvent(bool isEntering)
    {
        if (isEntering)
        {
            EventBus.Instance.Publish(new SystemStateEvent() { State = SystremState.ListeningLongSentence.ToString() });
        }
        else
        {
            EventBus.Instance.Publish(new SystemStateEvent() { State = SystremState.Listening.ToString() });
        }
    }

    public void StopListening()
    {
        if (_isDisposed) return;

        try
        {
            _cts?.Cancel();
            _waveIn?.StopRecording();
            _isListening = false;

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
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_isDisposed)
            return;

        if (disposing)
        {
            // Dispose managed resources
            _cts?.Cancel();
            _waveIn?.Dispose();
            _whisperProcessor?.Dispose();
            _whisperFactory?.Dispose();
            _cts?.Dispose();
            _processingLock?.Dispose();
        }

        // Dispose unmanaged resources if any

        _isDisposed = true;
        _logger.LogDeviceStatus("stopping", "Transcriber disposed successfully");
    }

    ~WhisperTranscriber()
    {
        Dispose(false);
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
