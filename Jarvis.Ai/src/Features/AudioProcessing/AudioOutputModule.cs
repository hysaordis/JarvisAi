using System.Net.Http.Headers;
using System.Text;
using Jarvis.Ai.Interfaces;
using NAudio.Wave;
using Newtonsoft.Json;

namespace Jarvis.Ai.Features.AudioProcessing;

public class AudioOutputModule : IAudioOutputModule
{
    private readonly IJarvisLogger _jarvisLogger;
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _defaultVoice;

    public AudioOutputModule(
        IJarvisLogger jarvisLogger,
        IJarvisConfigManager configManager)
    {
        _jarvisLogger = jarvisLogger;
        _httpClient = new HttpClient();
        _apiKey = configManager.GetValue("OPENAI_API_KEY")
                  ?? throw new ArgumentNullException("OPENAI_API_KEY is not configured");
        _defaultVoice = configManager.GetValue("OPENAI_TTS_VOICE") ?? "nova";
    }

    private async Task<Stream> GenerateSpeechAsync(
        string text,
        string model = "tts-1",
        CancellationToken cancellationToken = default)
    {
        try
        {
            _jarvisLogger.LogInformation($"Generating speech for text: {text}");

            if (text.Length > 4096)
            {
                _jarvisLogger.LogWarning("Text exceeds 4096 characters, truncating...");
                text = text[..4096];
            }

            var requestBody = new
            {
                model,
                input = text,
                voice = _defaultVoice,
                response_format = "mp3"
            };

            var jsonContent = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/audio/speech")
            {
                Content = content
            };

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

            var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new Exception($"OpenAI API request failed: {errorContent}");
            }

            return await response.Content.ReadAsStreamAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _jarvisLogger.LogError($"Error generating speech: {ex.Message}");
            throw;
        }
    }

    public async Task PlayAudioAsync(byte[] audioData, CancellationToken cancellationToken = default)
    {
        if (audioData == null || audioData.Length == 0)
        {
            _jarvisLogger.LogError("Invalid audio data provided");
            throw new ArgumentException("Audio data cannot be null or empty", nameof(audioData));
        }

        _jarvisLogger.LogInformation($"Playing audio data of {audioData.Length} bytes");

        try
        {
            var tcs = new TaskCompletionSource<bool>();

            using var ms = new MemoryStream(audioData);
            await using var mp3Reader = new Mp3FileReader(ms);
            using var waveOut = new WaveOutEvent();

            waveOut.Init(mp3Reader);
            waveOut.PlaybackStopped += (s, e) =>
            {
                _jarvisLogger.LogInformation("Playback stopped");
                if (e.Exception != null)
                {
                    tcs.SetException(e.Exception);
                }
                else
                {
                    tcs.SetResult(true);
                }
            };

            waveOut.Play();
            _jarvisLogger.LogInformation("Playback started");

            await using (cancellationToken.Register(() =>
                         {
                             _jarvisLogger.LogInformation("Playback cancelled");
                             waveOut.Stop();
                         }))
            {
                await tcs.Task;
            }
        }
        catch (OperationCanceledException)
        {
            _jarvisLogger.LogInformation("Audio playback was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _jarvisLogger.LogError($"Error during audio playback: {ex.Message}");
            throw;
        }
    }

    public async Task SpeakAsync(string text, CancellationToken cancellationToken)
    {
        _jarvisLogger.LogInformation($"Speaking text: {text}");

        try
        {
            await using var audioStream = await GenerateSpeechAsync(
                text: text,
                cancellationToken: cancellationToken
            );

            using var memoryStream = new MemoryStream();
            await audioStream.CopyToAsync(memoryStream, cancellationToken);
            var audioData = memoryStream.ToArray();

            await PlayAudioAsync(audioData, cancellationToken);
        }
        catch (Exception ex)
        {
            _jarvisLogger.LogError($"Error in SpeakAsync: {ex.Message}");
            throw;
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}