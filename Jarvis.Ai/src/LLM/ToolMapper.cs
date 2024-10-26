using Newtonsoft.Json;
using OllamaSharp.Models.Chat;

namespace Jarvis.Ai.LLM;

public class OpenAITool
{
    [JsonProperty("type")]
    public string Type { get; set; } = "function";

    [JsonProperty("function")]
    public OpenAIFunction Function { get; set; }
}

public class OpenAIFunction
{
    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("description")]
    public string Description { get; set; }

    [JsonProperty("parameters")]
    public OpenAIParameters Parameters { get; set; }
}

public class OpenAIParameters
{
    [JsonProperty("type")]
    public string Type { get; set; } = "object";

    [JsonProperty("properties")]
    public Dictionary<string, OpenAIProperty> Properties { get; set; }

    [JsonProperty("required")]
    public List<string> Required { get; set; }

    [JsonProperty("additionalProperties")]
    public bool AdditionalProperties { get; set; } = false;
}

public class OpenAIProperty
{
    [JsonProperty("type")]
    public string Type { get; set; }

    [JsonProperty("description")]
    public string Description { get; set; }

    [JsonProperty("enum", NullValueHandling = NullValueHandling.Ignore)]
    public List<string> Enum { get; set; }
}
public static class ToolMapper
{
    public static OpenAITool ConvertToOpenAiTool(OllamaSharp.Models.Chat.Tool ollamaTool)
    {
        if (ollamaTool.Function == null)
        {
            throw new ArgumentException("Ollama tool function cannot be null");
        }

        return new OpenAITool
        {
            Type = "function",
            Function = new OpenAIFunction
            {
                Name = ollamaTool.Function.Name,
                Description = ollamaTool.Function.Description,
                Parameters = ollamaTool.Function.Parameters != null
                    ? new OpenAIParameters
                    {
                        Type = "object",
                        Properties = ConvertProperties(ollamaTool.Function.Parameters.Properties),
                        Required = ollamaTool.Function.Parameters.Required?.ToList(),
                        AdditionalProperties = false
                    }
                    : null
            }
        };
    }

    private static Dictionary<string, OpenAIProperty> ConvertProperties(Dictionary<string, Properties> ollamaProperties)
    {
        if (ollamaProperties == null) return new Dictionary<string, OpenAIProperty>();

        return ollamaProperties.ToDictionary(
            kvp => kvp.Key,
            kvp => new OpenAIProperty
            {
                Type = kvp.Value.Type,
                Description = kvp.Value.Description,
                Enum = kvp.Value.Enum?.ToList()
            }
        );
    }

    public static List<OpenAITool> ConvertToOpenAiTools(IEnumerable<OllamaSharp.Models.Chat.Tool> ollamaTools)
    {
        if (ollamaTools == null) return new List<OpenAITool>();
        return ollamaTools.Select(ConvertToOpenAiTool).ToList();
    }
}