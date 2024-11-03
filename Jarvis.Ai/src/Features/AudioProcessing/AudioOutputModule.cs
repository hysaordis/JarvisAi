using System.Net.Http.Headers;
using System.Text;
using Jarvis.Ai.Core.Events;
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

        _jarvisLogger.LogDeviceStatus("initialized", $"Audio module ready with voice: {_defaultVoice}");
    }

    private async Task<Stream> GenerateSpeechAsync(
        string text,
        string model = "tts-1",
        CancellationToken cancellationToken = default)
    {
        try
        {
            _jarvisLogger.LogToolExecution("openai-tts", "start",
                $"Converting text to speech [Voice: {_defaultVoice}]");

            if (text.Length > 4096)
            {
                _jarvisLogger.LogToolExecution("openai-tts", "warning", "Text truncated to 4096 characters");
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

            _jarvisLogger.LogToolExecution("openai-tts", "processing", "Sending request to OpenAI API");
            var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _jarvisLogger.LogToolExecution("openai-tts", "error", $"API Error: {errorContent}");
                throw new Exception($"OpenAI API request failed: {errorContent}");
            }

            _jarvisLogger.LogToolExecution("openai-tts", "complete", "Text to speech conversion successful");
            return await response.Content.ReadAsStreamAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _jarvisLogger.LogTranscriberError("tts", $"Text to speech conversion failed: {ex.Message}");
            throw;
        }
    }

    public async Task PlayAudioAsync(byte[] audioData, CancellationToken cancellationToken = default)
    {
        if (audioData == null || audioData.Length == 0)
        {
            _jarvisLogger.LogDeviceStatus("error", "Invalid audio data provided");
            throw new ArgumentException("Audio data cannot be null or empty", nameof(audioData));
        }

        _jarvisLogger.LogAgentStatus("speaking", "Assistant is speaking");

        try
        {
            var tcs = new TaskCompletionSource<bool>();

            using var ms = new MemoryStream(audioData);
            await using var mp3Reader = new Mp3FileReader(ms);
            using var waveOut = new WaveOutEvent();

            waveOut.Init(mp3Reader);
            waveOut.PlaybackStopped += (s, e) =>
            {
                EventBus.Instance.Publish(new SystemStateEvent
                {
                    State = SystremState.Listening.ToString()
                });
                if (e.Exception != null)
                {
                    _jarvisLogger.LogAgentStatus("error", $"Assistant speech error: {e.Exception.Message}");
                    tcs.SetException(e.Exception);
                }
                else
                {
                    _jarvisLogger.LogAgentStatus("ready", "Assistant finished speaking");
                    tcs.SetResult(true);
                }
            };

            EventBus.Instance.Publish(new SystemStateEvent
            {
                State = SystremState.Playing.ToString()
            });

            waveOut.Play();

            await using (cancellationToken.Register(() =>
            {
                _jarvisLogger.LogAgentStatus("interrupted", "Assistant speech interrupted");
                waveOut.Stop();
            }))
            {
                await tcs.Task;
            }
        }
        catch (OperationCanceledException)
        {
            _jarvisLogger.LogAgentStatus("interrupted", "Assistant speech cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _jarvisLogger.LogAgentStatus("error", $"Assistant speech error: {ex.Message}");
            throw;
        }
    }

    public async Task SpeakAsync(string text, CancellationToken cancellationToken)
    {
        _jarvisLogger.LogToolExecution("tts", "start", "Converting assistant response to speech");

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
            _jarvisLogger.LogTranscriberError("tts", $"Failed to convert response to speech: {ex.Message}");
            throw;
        }
    }

    public void Dispose()
    {
        _jarvisLogger.LogDeviceStatus("stopping", "Disposing audio module");
        _httpClient.Dispose();
    }
}