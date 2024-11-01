using Whisper.net;
using NAudio.Wave;
using Jarvis.Ai.Common.Utils;
using Jarvis.Ai.Interfaces;
using System.Net.Http;

namespace Jarvis.Ai.Features.AudioProcessing;

public class WakeWordDetector : IDisposable
{
    private const int SAMPLE_RATE = 16000;
    private const float ACTIVATION_THRESHOLD = 0.7f;
    private const int BUFFER_SIZE = SAMPLE_RATE * 2; // 2 seconds buffer
    private const string MODEL_URL = "https://huggingface.co/sandrohanea/whisper.net/blob/main/classic/ggml-base.bin";
    private const string MODEL_FILENAME = "ggml-base.bin";
    private static readonly string MODEL_PATH = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "JarvisAI", MODEL_FILENAME);

    private readonly string[] _wakeWords = { "hey jarvis", "jarvis", "hey assistant" };
    private readonly WhisperFactory _whisperFactory;
    private readonly WhisperProcessor _whisperProcessor;
    private readonly RingBuffer<float> _audioBuffer;
    private readonly IJarvisLogger _logger;

    public event EventHandler<bool> WakeWordDetected;

    public WakeWordDetector(IJarvisLogger logger)
    {
        _logger = logger;
        _audioBuffer = new RingBuffer<float>(BUFFER_SIZE);

        EnsureModelDownloaded().Wait();

        _whisperFactory = WhisperFactory.FromPath(MODEL_PATH);
        _whisperProcessor = _whisperFactory.CreateBuilder()
            .WithLanguage("en")
            .WithProbabilities()
            .WithNoContext()
            .WithTemperature(0.0f)
            .Build();
    }

    private async Task EnsureModelDownloaded()
    {
        if (!File.Exists(MODEL_PATH))
        {
            _logger.LogDeviceStatus("downloading", "Downloading Whisper model for wake word detection...");
            Directory.CreateDirectory(Path.GetDirectoryName(MODEL_PATH));

            using (var httpClient = new HttpClient())
            using (var response = await httpClient.GetAsync(MODEL_URL))
            using (var fs = new FileStream(MODEL_PATH, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await response.Content.CopyToAsync(fs);
            }

            _logger.LogDeviceStatus("downloaded", "Whisper model downloaded successfully.");
        }
    }

    public async Task ProcessAudioSegment(float[] audioData)
    {
        _audioBuffer.Write(audioData, 0, audioData.Length);

        if (_audioBuffer.Count >= BUFFER_SIZE)
        {
            var segment = new float[BUFFER_SIZE];
            _audioBuffer.Read(segment, 0, BUFFER_SIZE);

            await foreach (var result in _whisperProcessor.ProcessAsync(segment))
            {
                var text = result.Text.Trim().ToLowerInvariant();
                if (string.IsNullOrEmpty(text)) continue;

                foreach (var wakeWord in _wakeWords)
                {
                    if (text.Contains(wakeWord) && result.Probability > ACTIVATION_THRESHOLD)
                    {
                        _logger.LogTranscriptionProgress("wake-word", $"Wake word detected: {text} ({result.Probability:F2})");
                        WakeWordDetected?.Invoke(this, true);
                        return;
                    }
                }
            }
        }
    }

    public void Dispose()
    {
        _whisperProcessor?.Dispose();
        _whisperFactory?.Dispose();
    }
}
