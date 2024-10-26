

using Jarvis.Ai.LLM;

namespace Jarvis.Ai
{
    public interface ILlmClient
    {
        Task<string> ChatPrompt(string prompt, string model = "gpt-4");
        Task<Message> SendCommandToLlmAsync(List<Message> messages, CancellationToken cancellationToken);
        Task<T> StructuredOutputPrompt<T>(string prompt, string model = "gpt-4") where T : class;
    }
}