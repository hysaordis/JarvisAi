

namespace Jarvis.Ai
{
    public interface ILlmClient
    {
        Task<string> ChatPrompt(string prompt, string model = "gpt-4");
        Task<ResponseMessage> SendCommandToLlmAsync(List<ResponseMessage> messages, CancellationToken cancellationToken);
        Task<T> StructuredOutputPrompt<T>(string prompt, string model = "gpt-4") where T : class;
    }
}