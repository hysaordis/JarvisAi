namespace Jarvis.Ai.Interfaces;

public interface IAudioOutputModule
{
    Task PlayAudioAsync(byte[] audioData, CancellationToken cancellationToken = default);
}