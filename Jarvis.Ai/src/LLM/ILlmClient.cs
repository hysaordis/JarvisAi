using Newtonsoft.Json;

namespace Jarvis.Ai
{
    public interface ILlmClient
    {
        Task<string> ChatPrompt(string prompt, string model = "gpt-4");
        Task<Message> SendCommandToLlmAsync(List<Message> messages, CancellationToken cancellationToken);
        Task<T> StructuredOutputPrompt<T>(string prompt, string model = "gpt-4") where T : class;
    }

    public class Message
    {
        [JsonProperty("role")]
        public string Role { get; set; }

        [JsonProperty("content", NullValueHandling = NullValueHandling.Ignore)]
        public string Content { get; set; }

        [JsonProperty("tool_calls", NullValueHandling = NullValueHandling.Ignore)]
        public List<FunctionCall> ToolCalls { get; set; }

        [JsonProperty("tool_call_id", NullValueHandling = NullValueHandling.Ignore)]
        public string ToolCallId { get; set; }
    }

    public class FunctionCall
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("function")]
        public FunctionDetail Function { get; set; }
    }

    public class FunctionDetail
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("arguments")]
        public Dictionary<string, object> Arguments { get; set; }
    }
}