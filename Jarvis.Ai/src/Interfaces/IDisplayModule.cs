namespace Jarvis.Ai.Interfaces
{
    public interface IDisplayModule
    {
        Task ShowAsync(string message, CancellationToken cancellationToken);
    }
}
