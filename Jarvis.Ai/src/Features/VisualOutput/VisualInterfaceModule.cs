using Jarvis.Ai.Interfaces;

namespace Jarvis.Ai.Features.VisualOutput
{
    public class VisualInterfaceModule : IDisplayModule
    {
        public Task ShowAsync(string message, CancellationToken cancellationToken)
        {
            Console.WriteLine(message);
            return Task.CompletedTask;
        }
    }
}
