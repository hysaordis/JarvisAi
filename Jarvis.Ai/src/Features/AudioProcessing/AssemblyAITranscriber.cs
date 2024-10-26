using NAudio.Wave;
using AssemblyAI;
using AssemblyAI.Transcripts;
using System.Text;
using Jarvis.Ai.Features.AudioProcessing;
using Jarvis.Ai.Interfaces;

public sealed class AssemblyAITranscriber : ITranscriber, IDisposable
{
    #region Constants

    /// <summary>
    /// Audio sampling rate in Hz. 44100 Hz is CD quality audio.
    /// </summary>
    private const int SAMPLE_RATE = 44100;

    /// <summary>
    /// Number of bits used to represent each audio sample.
    /// 16-bit is standard for most audio applications.
    /// </summary>
    private const int BITS_PER_SAMPLE = 16;

    /// <summary>
    /// Number of audio channels. 1 for mono recording.
    /// </summary>
    private const int CHANNELS = 1;

    /// <summary>
    /// Size of the audio buffer in milliseconds.
    /// Lower values mean more frequent updates but more CPU usage.
    /// </summary>
    private const int BUFFER_MILLISECONDS = 50;

    /// <summary>
    /// Maximum size of the audio buffer in samples.
    /// 30 seconds of audio at SAMPLE_RATE frequency.
    /// </summary>
    private const int MAX_QUEUE_SIZE = SAMPLE_RATE * 30;

    /// <summary>
    /// Default audio input device number.
    /// 0 typically represents the default system recording device.
    /// </summary>
    private const int DEVICE_NUMBER = 0;

    /// <summary>
    /// Configuration key for AssemblyAI API key.
    /// </summary>
    private const string API_KEY_CONFIG_NAME = "ASSEMBLYAI_API_KEY";

    #region Voice Activity Detection (VAD) Parameters

    /// <summary>
    /// Energy threshold below which audio is considered silence.
    /// Range: 0.0 to 1.0, where 0.01 is a good starting point for most environments.
    /// Increase this value in noisy environments.
    /// </summary>
    private const float SILENCE_THRESHOLD = 0.01f;

    /// <summary>
    /// Duration of silence in milliseconds before considering speech ended.
    /// 1000ms (1 second) allows for natural pauses in speech without cutting off.
    /// </summary>
    private const int SILENCE_DURATION_MS = 1000;

    /// <summary>
    /// Minimum duration of speech in milliseconds to be considered valid.
    /// 500ms helps filter out short noises and partial words.
    /// </summary>
    private const int MIN_SPEECH_DURATION_MS = 500;

    #endregion

    #endregion

    #region Private Fields

    /// <summary>
    /// Client for interacting with AssemblyAI API.
    /// </summary>
    private readonly AssemblyAIClient _assemblyAIClient;

    /// <summary>
    /// Circular buffer for storing audio samples during speech.
    /// </summary>
    private readonly RingBuffer<float> _audioBuffer;

    /// <summary>
    /// Builds the complete transcription text over time.
    /// </summary>
    private readonly StringBuilder _transcriptionBuilder;

    /// <summary>
    /// Logger for diagnostic and troubleshooting purposes.
    /// </summary>
    private readonly IJarvisLogger _logger;

    /// <summary>
    /// Directory for temporary audio files before upload.
    /// </summary>
    private readonly string _tempDirectory;

    /// <summary>
    /// NAudio recording device interface.
    /// </summary>
    private WaveInEvent _waveIn;

    /// <summary>
    /// Cancellation token source for stopping async operations.
    /// </summary>
    private CancellationTokenSource _cts;

    /// <summary>
    /// Flags for tracking transcriber state
    /// </summary>
    private bool _isInitialized;

    private bool _isDisposed;
    private bool _isSpeaking;
    private bool _isProcessing;

    /// <summary>
    /// Timestamps for speech detection
    /// </summary>
    private DateTime _lastSpeechTime;

    private DateTime _silenceStartTime;

    /// <summary>
    /// Counter for collected speech samples
    /// </summary>
    private int _speechSampleCount;

    #endregion

    public event EventHandler<string> OnTranscriptionResult;
    public event EventHandler<Exception> OnError;

    public AssemblyAITranscriber(IJarvisLogger logger, IJarvisConfigManager configManager)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var apiKey = configManager.GetValue(API_KEY_CONFIG_NAME)
                     ?? throw new ArgumentNullException(API_KEY_CONFIG_NAME);

        _assemblyAIClient = new AssemblyAIClient(apiKey);
        _audioBuffer = new RingBuffer<float>(MAX_QUEUE_SIZE);
        _transcriptionBuilder = new StringBuilder();
        _tempDirectory = Path.Combine(Path.GetTempPath(), "AssemblyAITranscriber");
        Directory.CreateDirectory(_tempDirectory);
    }

    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (_isInitialized)
        {
            _logger.LogWarning("Transcriber is already initialized.");
            return Task.CompletedTask;
        }

        try
        {
            InitializeAudioDevice();
            _isInitialized = true;
            _logger.LogInformation("Transcriber initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to initialize transcriber: {ex.Message}");
            OnError?.Invoke(this, ex);
            throw;
        }

        return Task.CompletedTask;
    }

    private void InitializeAudioDevice()
    {
        try
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
        catch (Exception ex)
        {
            _logger.LogError($"Failed to initialize audio device: {ex.Message}");
            throw;
        }
    }

    public void StartListening()
    {
        ThrowIfNotInitialized();
        ThrowIfDisposed();

        try
        {
            _cts = new CancellationTokenSource();
            _waveIn.StartRecording();
            _logger.LogInformation("Started listening for audio input");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to start listening: {ex.Message}");
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
            _logger.LogError($"Error while stopping listening: {ex.Message}");
            OnError?.Invoke(this, ex);
        }
    }

    /// <summary>
    /// Processes incoming audio data and detects voice activity.
    /// Uses energy-based Voice Activity Detection (VAD) to identify speech segments.
    /// </summary>
    /// <param name="sender">The source of the audio data.</param>
    /// <param name="e">Event args containing the audio buffer data.</param>
    private void OnDataAvailable(object sender, WaveInEventArgs e)
    {
        try
        {
            // Calculate number of samples from the byte buffer
            int bytesRecorded = e.BytesRecorded;
            int bytesPerSample = _waveIn.WaveFormat.BitsPerSample / 8;
            int sampleCount = bytesRecorded / bytesPerSample;
            float[] samples = new float[sampleCount];

            // Convert bytes to float samples and calculate signal energy
            float energy = 0;
            for (int i = 0; i < sampleCount; i++)
            {
                short sample = BitConverter.ToInt16(e.Buffer, i * bytesPerSample);
                samples[i] = sample / 32768f; // Normalize to [-1, 1] range
                energy += Math.Abs(samples[i]);
            }

            energy /= sampleCount; // Average energy per sample

            // Detect voice activity based on energy threshold
            bool isCurrentlySpeaking = energy > SILENCE_THRESHOLD;

            if (isCurrentlySpeaking)
            {
                if (!_isSpeaking) // Speech just started
                {
                    _isSpeaking = true;
                    _lastSpeechTime = DateTime.Now;
                    _logger?.LogInformation("Voice activity detected");
                }

                _silenceStartTime = DateTime.Now;
            }
            else if (_isSpeaking) // Was speaking, check if enough silence to stop
            {
                var silenceDuration = DateTime.Now - _silenceStartTime;
                if (silenceDuration.TotalMilliseconds >= SILENCE_DURATION_MS)
                {
                    _isSpeaking = false;
                    var speechDuration = _silenceStartTime - _lastSpeechTime;

                    // Process if speech duration meets minimum requirement
                    if (speechDuration.TotalMilliseconds >= MIN_SPEECH_DURATION_MS && !_isProcessing)
                    {
                        _isProcessing = true;
                        Task.Run(async () => await ProcessSpeechSegmentAsync(_cts.Token));
                    }
                }
            }

            // Store samples only during speech
            if (_isSpeaking)
            {
                _audioBuffer.Write(samples, 0, samples.Length);
                _speechSampleCount += samples.Length;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error processing audio data: {ex.Message}");
            OnError?.Invoke(this, ex);
        }
    }

    /// <summary>
    /// Processes a complete speech segment by converting it to a WAV file,
    /// uploading to AssemblyAI, and obtaining the transcription.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation if needed.</param>
    private async Task ProcessSpeechSegmentAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Ignore segments shorter than 1 second
            if (_speechSampleCount < SAMPLE_RATE)
            {
                _isProcessing = false;
                return;
            }

            // Read collected audio data from buffer
            var audioData = new float[_speechSampleCount];
            _audioBuffer.Read(audioData, 0, _speechSampleCount);
            _speechSampleCount = 0;

            // Create temporary WAV file
            string tempFilePath = Path.Combine(_tempDirectory, $"audio_{DateTime.Now:yyyyMMddHHmmss}.wav");
            await using (var writer =
                         new WaveFileWriter(tempFilePath, new WaveFormat(SAMPLE_RATE, BITS_PER_SAMPLE, CHANNELS)))
            {
                // Convert float samples to 16-bit PCM
                var byteData = new byte[audioData.Length * 2];
                for (int i = 0; i < audioData.Length; i++)
                {
                    var sample = (short)(audioData[i] * 32768f);
                    var bytes = BitConverter.GetBytes(sample);
                    byteData[i * 2] = bytes[0];
                    byteData[i * 2 + 1] = bytes[1];
                }

                writer.Write(byteData, 0, byteData.Length);
            }

            // Upload and transcribe
            var uploadedFile =
                await _assemblyAIClient.Files.UploadAsync(new FileInfo(tempFilePath),
                    cancellationToken: cancellationToken);

            var transcriptParams = new TranscriptParams
            {
                AudioUrl = uploadedFile.UploadUrl,
                LanguageCode = TranscriptLanguageCode.En,
                Punctuate = true,
                FormatText = true
            };

            _logger.LogInformation("Calling AssemblyAI API");

            var transcript =
                await _assemblyAIClient.Transcripts.TranscribeAsync(transcriptParams,
                    cancellationToken: cancellationToken);
            transcript.EnsureStatusCompleted();

            // Process transcription result
            if (!string.IsNullOrEmpty(transcript.Text))
            {
                _transcriptionBuilder.AppendLine(transcript.Text);
                OnTranscriptionResult?.Invoke(this, _transcriptionBuilder.ToString().Trim());
            }

            // Cleanup
            try
            {
                File.Delete(tempFilePath);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error deleting temporary file: {ex.Message}");
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError($"Error processing speech segment: {ex.Message}");
            OnError?.Invoke(this, ex);
        }
        finally
        {
            _isProcessing = false;
        }
    }

    private void OnRecordingStopped(object sender, StoppedEventArgs e)
    {
        if (e.Exception != null)
        {
            _logger.LogError($"Recording stopped due to error: {e.Exception.Message}");
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
            throw new ObjectDisposedException(nameof(AssemblyAITranscriber));
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;

        try
        {
            StopListening();
            _waveIn?.Dispose();
            _cts?.Dispose();

            try
            {
                Directory.Delete(_tempDirectory, true);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error cleaning temporary directory: {ex.Message}");
            }

            _isDisposed = true;
            _logger.LogInformation("Transcriber disposed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error during disposal: {ex.Message}");
        }
    }
}