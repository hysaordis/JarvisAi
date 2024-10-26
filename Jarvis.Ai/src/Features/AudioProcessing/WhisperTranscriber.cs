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
    private const int BUFFER_SECONDS = 3;
    private const int SAMPLE_RATE = 16000;
    private const int BITS_PER_SAMPLE = 16;
    private const int CHANNELS = 1;
    private const int BUFFER_MILLISECONDS = 50;
    private const int MAX_QUEUE_SIZE = SAMPLE_RATE * 5;

    #endregion

    #region Private Fields
    private readonly string _whisperModelPath;
    private readonly IJarvisLogger _logger;
    private readonly IJarvisConfigManager _configManager;
    private readonly StringBuilder _transcriptionBuilder;

    private readonly RingBuffer<float> _audioBuffer;

    private WhisperFactory _whisperFactory;
    private WhisperProcessor _whisperProcessor;
    private WaveInEvent _waveIn;
    private CancellationTokenSource _cts;
    private bool _isPotentiallyComplete;
    private bool _isInitialized;
    private bool _isDisposed;
    #endregion

    #region Events
    public event EventHandler<string> OnTranscriptionResult;
    public event EventHandler<Exception> OnError;
    #endregion

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
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (_isInitialized)
        {
            _logger.LogWarning("Transcriber is already initialized.");
            return;
        }

        try
        {
            await Task.Run(() =>
            {
                _whisperFactory = WhisperFactory.FromPath(_whisperModelPath);

                _whisperProcessor = _whisperFactory.CreateBuilder()
                    .WithLanguage("auto")
                    .Build();

            }, cancellationToken);

            InitializeAudioDevice();
            _isInitialized = true;
            _logger.LogInformation("Transcriber initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to initialize transcriber: {Message}", ex.Message);
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
                catch (Exception ex)
                {
                    _logger.LogError("Error in processing task: {Message}", ex.Message);
                    OnError?.Invoke(this, ex);
                }
            }, _cts.Token);

            _logger.LogInformation("Started listening for audio input");
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to start listening: {Message}", ex.Message);
            OnError?.Invoke(this, ex);
            throw;
        }
    }

    public void StopListening()
    {
        if (_isDisposed) return;

        try
        {
            _cts?.Cancel();
            _waveIn?.StopRecording();
            _logger.LogInformation("Stopped listening for audio input");
        }
        catch (Exception ex)
        {
            _logger.LogError("Error while stopping listening: {Message}", ex.Message);
            OnError?.Invoke(this, ex);
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

            _audioBuffer.Write(samples, 0, samples.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError("Errore nell'elaborazione dei dati audio: {Message}", ex.Message);
            OnError?.Invoke(this, ex);
        }
    }

    private async Task ProcessAudioAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var samplesNeeded = SAMPLE_RATE * BUFFER_SECONDS;
            if (_audioBuffer.Count >= samplesNeeded)
            {
                var audioData = new float[samplesNeeded];
                _audioBuffer.Read(audioData, 0, samplesNeeded);

                await ProcessSegmentAsync(audioData, cancellationToken);
            }

            await Task.Delay(BUFFER_MILLISECONDS, cancellationToken);
        }
    }
    
    private async Task ProcessSegmentAsync(float[] audioData, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var segment in _whisperProcessor.ProcessAsync(audioData, cancellationToken))
            {
                var text = segment.Text.Trim().RemoveTagsPreserveContent();
                if (string.IsNullOrEmpty(text))
                    continue;

                _transcriptionBuilder.AppendLine(text);

                OnTranscriptionResult?.Invoke(this, _transcriptionBuilder.ToString());
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Error processing audio segment: {Message}", ex.Message);
            OnError?.Invoke(this, ex);
        }
    }


    private void HandleTranscriptionCompletion()
    {
        var completeTranscription = _transcriptionBuilder.ToString().Trim();
        if (!string.IsNullOrWhiteSpace(completeTranscription))
        {
            OnTranscriptionResult?.Invoke(this, completeTranscription);
        }

        // Reset state
        _transcriptionBuilder.Clear();
        _isPotentiallyComplete = false;
    }

    private void OnRecordingStopped(object sender, StoppedEventArgs e)
    {
        if (e.Exception != null)
        {
            _logger.LogError("Recording stopped due to error: {Message}", e.Exception.Message);
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
            
            _isDisposed = true;
            _logger.LogInformation("Transcriber disposed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError("Error during disposal: {Message}", ex.Message);
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
