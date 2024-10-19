namespace Jarvis.Ai.Interfaces
{
    public interface IJarvis
    {
        Task InitializeAsync(string[]? initialCommands, CancellationToken cancellationToken);
        Task ProcessAudioInputAsync(byte[] audioData, CancellationToken cancellationToken);
        Task<string> ListenForResponseAsync(CancellationToken cancellationToken);
        Task ExecuteCommandsAsync(string[] commands, CancellationToken cancellationToken);
        Task ShutdownAsync();
    }
}
