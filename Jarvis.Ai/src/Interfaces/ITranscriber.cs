namespace Jarvis.Ai.Interfaces
{
    public interface ITranscriber
    {
        Task InitializeAsync(CancellationToken cancellationToken);
        void StartListening();
        void StopListening();
        event EventHandler<string> OnTranscriptionResult;
    }
}
