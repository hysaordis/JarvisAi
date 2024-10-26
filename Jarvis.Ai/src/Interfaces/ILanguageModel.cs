namespace Jarvis.Ai.Interfaces
{
    public interface ILanguageModel
    {
        Task InitializeAsync(CancellationToken cancellationToken);
        Task<string> ProcessInputAsync(string input, CancellationToken cancellationToken);
        Task ShutdownAsync();
    }
}
