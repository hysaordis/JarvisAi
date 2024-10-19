using Jarvis.Ai.Common.Settings;
using Jarvis.Ai.Interfaces;
using Jarvis.Ai.src.Interfaces;
using NAudio.Wave;

namespace Jarvis.Ai.Features.AudioProcessing;

public class AudioOutputModule : IAudioOutputModule
{
    private readonly IJarvisLogger _jarvisLogger;

    public AudioOutputModule(IJarvisLogger jarvisLogger)
    {
        _jarvisLogger = jarvisLogger;
    }

    public async Task PlayAudioAsync(byte[] audioData, CancellationToken cancellationToken = default)
    {
        if (audioData == null || audioData.Length == 0)
        {
            _jarvisLogger.LogError("Invalid audio data provided");
            throw new ArgumentException("Audio data cannot be null or empty", nameof(audioData));
        }

        _jarvisLogger.LogInformation($"Entering PlayAudioAsync with {audioData.Length} bytes of audio data");

        try
        {
            var tcs = new TaskCompletionSource<bool>();

            using (var waveOut = new WaveOutEvent())
            using (var ms = new MemoryStream(audioData))
            {
                var waveFormat = new WaveFormat(Constants.RATE, Constants.BIT, Constants.CHANNELS);
                using (var rdr = new RawSourceWaveStream(ms, waveFormat))
                {
                    waveOut.Init(rdr);

                    waveOut.PlaybackStopped += (s, e) =>
                    {
                        _jarvisLogger.LogInformation("PlaybackStopped event fired");
                        if (e.Exception != null)
                        {
                            _jarvisLogger.LogError($"Error during playback: {e.Exception.Message}");
                            tcs.SetException(e.Exception);
                        }
                        else
                        {
                            tcs.SetResult(true);
                        }
                    };

                    waveOut.Play();
                    _jarvisLogger.LogInformation("Playback started");

                    // Await playback completion or cancellation
                    using (cancellationToken.Register(() =>
                           {
                               _jarvisLogger.LogInformation("Playback cancelled");
                               waveOut.Stop();
                           }))
                    {
                        await Task.WhenAny(tcs.Task, Task.Delay(Timeout.Infinite, cancellationToken));
                        cancellationToken.ThrowIfCancellationRequested();
                    }
                }
            }

            _jarvisLogger.LogInformation("Audio playback completed and resources disposed");
        }
        catch (OperationCanceledException)
        {
            _jarvisLogger.LogInformation("Audio playback was cancelled");
        }
        catch (Exception ex)
        {
            _jarvisLogger.LogError($"Exception in PlayAudioAsync: {ex}");
            throw;
        }
    }
}