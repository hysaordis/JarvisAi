using System.Speech.Synthesis;
using Jarvis.Ai.Interfaces;
using System.Runtime.InteropServices;
using NAudio.Wave;

namespace Jarvis.Ai.Features.AudioProcessing;

public class WindowsAudioOutputModule : IAudioOutputModule, IDisposable
{
    private readonly IJarvisLogger _jarvisLogger;
    private readonly SpeechSynthesizer _synthesizer;
    private readonly string _defaultVoiceName;
    private bool _disposed;

    public WindowsAudioOutputModule(
        IJarvisLogger jarvisLogger,
        IJarvisConfigManager configManager)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            throw new PlatformNotSupportedException("WindowsAudioOutputModule is only supported on Windows.");

        _jarvisLogger = jarvisLogger;
        _synthesizer = new SpeechSynthesizer();
        _defaultVoiceName = configManager.GetValue("WINDOWS_TTS_VOICE") ?? "Microsoft David Desktop";

        try
        {
            _synthesizer.SelectVoice(_defaultVoiceName);
            _synthesizer.Rate = 0; // Normal speed
            _synthesizer.Volume = 100; // Maximum volume

            _jarvisLogger.LogDeviceStatus("initialized", $"Windows Audio module ready with voice: {_defaultVoiceName}");
        }
        catch (Exception ex)
        {
            _jarvisLogger.LogDeviceStatus("error", $"Failed to initialize voice {_defaultVoiceName}: {ex.Message}");
            throw;
        }
    }

    public async Task PlayAudioAsync(byte[] audioData, CancellationToken cancellationToken = default)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(WindowsAudioOutputModule));
        
        try
        {
            var tcs = new TaskCompletionSource<bool>();

            using var ms = new MemoryStream(audioData);
            using var waveOut = new WaveOutEvent();
            using var mp3Reader = new Mp3FileReader(ms);

            waveOut.Init(mp3Reader);
            waveOut.PlaybackStopped += (s, e) =>
            {
                if (e.Exception != null)
                    tcs.SetException(e.Exception);
                else
                    tcs.SetResult(true);
            };

            using var registration = cancellationToken.Register(() =>
            {
                waveOut.Stop();
                tcs.TrySetCanceled();
            });

            _jarvisLogger.LogAgentStatus("speaking", "Playing audio");
            waveOut.Play();
            await tcs.Task;
            _jarvisLogger.LogAgentStatus("complete", "Audio playback completed");
        }
        catch (OperationCanceledException)
        {
            _jarvisLogger.LogAgentStatus("cancelled", "Audio playback cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _jarvisLogger.LogAgentStatus("error", $"Audio playback error: {ex.Message}");
            throw;
        }
    }

    public async Task SpeakAsync(string text, CancellationToken cancellationToken)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(WindowsAudioOutputModule));

        try
        {
            var tcs = new TaskCompletionSource<bool>();

            _synthesizer.SpeakCompleted += (s, e) =>
            {
                if (e.Error != null)
                    tcs.SetException(e.Error);
                else
                    tcs.SetResult(true);
            };

            using var registration = cancellationToken.Register(() =>
            {
                _synthesizer.SpeakAsyncCancelAll();
                tcs.TrySetCanceled();
            });

            _jarvisLogger.LogAgentStatus("speaking", "Starting speech synthesis");
            _synthesizer.SpeakAsync(text);
            await tcs.Task;
            _jarvisLogger.LogAgentStatus("complete", "Speech synthesis completed");
        }
        catch (OperationCanceledException)
        {
            _jarvisLogger.LogAgentStatus("cancelled", "Speech synthesis cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _jarvisLogger.LogAgentStatus("error", $"Speech synthesis error: {ex.Message}");
            throw;
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _synthesizer.Dispose();
                _jarvisLogger.LogDeviceStatus("disposed", "Windows Audio module disposed");
            }
            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
