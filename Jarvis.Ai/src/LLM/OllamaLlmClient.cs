using System.Reflection;
using System.Text;
using Jarvis.Ai.Interfaces;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OllamaSharp;
using OllamaSharp.Models;
using OllamaSharp.Models.Chat;

namespace Jarvis.Ai.LLM;

public class OllamaLlmClient : ILlmClient
{
    private readonly ILogger<OllamaLlmClient> _logger;
    private readonly OllamaApiClient _ollamaClient;
    private readonly string _modelName;
    private readonly IJarvisConfigManager _configManager;
    private readonly IStarkArsenal _starkArsenal;

    public OllamaLlmClient(
       ILogger<OllamaLlmClient> logger,
       IJarvisConfigManager configManager,
       IStarkArsenal starkArsenal)
    {
        _logger = logger;
        _configManager = configManager;
        _starkArsenal = starkArsenal;
        _modelName = configManager.GetValue("OLLAMA_MODEL_NAME") ?? "llama3.2";

        var baseUrl = configManager.GetValue("OLLAMA_BASE_URL") ?? "http://localhost:11434";
        var uri = new Uri(baseUrl);
        _ollamaClient = new OllamaApiClient(uri)
        {
            SelectedModel = _modelName
        };
    }

    private string ParseRole(string role)
    {
        return role?.ToLower() switch
        {
            "user" => "user",
            "assistant" => "assistant",
            "system" => "system",
            _ => "user" // Default to user for unknown roles
        };
    }

    private ResponseMessage ParseOllamaResponse(ChatResponseStream response)
    {
        if (response?.Message == null) return null;

        return new ResponseMessage
        {
            Role = response.Message.Role?.ToString().ToLower() ?? "assistant",
            Content = response.Message.Content ?? string.Empty
        };
    }

    public async Task<ResponseMessage> SendCommandToLlmAsync(List<ResponseMessage> messages, CancellationToken cancellationToken)
    {
        try
        {
            List<Tool> toolsArray = _starkArsenal.GetToolsForOllama();

            // Convert input messages to Ollama format with safe Role parsing
            var ollamaMessages = messages.Select(m => new Message
            {
                Role = ParseRole(m.Role),
                Content = m.Content
            }).ToList();

            var chatRequest = new ChatRequest
            {
                Model = _modelName,
                Messages = ollamaMessages,
                Tools = toolsArray,
                Stream = false,
                KeepAlive = "5m"
            };

            _logger.LogInformation("Sending request to Ollama via OllamaSharp");
            ResponseMessage responseMessage = null;

            await foreach (var responseChunk in _ollamaClient.Chat(chatRequest, cancellationToken))
            {
                if (responseChunk is null) continue;

                if (responseChunk.Done)
                {
                    responseMessage = ParseOllamaResponse(responseChunk);
                    break;
                }
                else if (responseMessage == null)
                {
                    responseMessage = new ResponseMessage
                    {
                        Role = responseChunk.Message?.Role?.ToString().ToLower() ?? "assistant",
                        Content = responseChunk.Message?.Content ?? string.Empty
                    };
                }
                else
                {
                    // Accumulate content for non-tool responses
                    responseMessage.Content += responseChunk.Message?.Content ?? string.Empty;
                }
            }

            return responseMessage ?? new ResponseMessage
            {
                Role = "assistant",
                Content = "No response generated"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SendToOllamaAsync");
            throw;
        }
    }

    public async Task<T> StructuredOutputPrompt<T>(string prompt, string model = "llama3.2") where T : class
    {
        try
        {
            var jsonStructure = CreateJsonStructure<T>();
            var systemPrompt = "You are a helpful assistant that always responds with valid JSON in the specified format.";

            var formattedPrompt = $@"{prompt}

<convert-instructions>
    <instruction>Provide your response in the following JSON format.</instruction>
</convert-instructions>
<json-format>
{jsonStructure}
</json-format>";

            _logger.LogInformation("Sending structured output prompt to Ollama");

            var request = new GenerateRequest
            {
                Model = "llama3.2",
                Prompt = formattedPrompt,
                System = systemPrompt,
                Stream = false,
                Format = "json",
                KeepAlive = "5m"
            };

            var responseContentBuilder = new StringBuilder();

            await foreach (var responseChunk in _ollamaClient.Generate(request, CancellationToken.None))
            {
                if (responseChunk != null && !string.IsNullOrEmpty(responseChunk.Response))
                {
                    responseContentBuilder.Append(responseChunk.Response);
                }
                if (responseChunk.Done)
                {
                    var result = responseChunk.Response;
                }
            }

            var responseContent = responseContentBuilder.ToString();
            _logger.LogInformation($"Received response: {responseContent}");

            // Clean and parse the response
            responseContent = CleanJsonResponse(responseContent);

            var settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            };

            return JsonConvert.DeserializeObject<T>(responseContent, settings)
                   ?? throw new Exception("Failed to deserialize response");
        }
        catch (JsonException ex)
        {
            _logger.LogError($"Failed to parse Ollama response as JSON: {ex.Message}");
            throw new Exception($"Failed to parse Ollama response as JSON: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Unexpected error while processing Ollama response: {ex.Message}");
            throw new Exception($"Unexpected error while processing Ollama response: {ex.Message}");
        }
    }

    public async Task<string> ChatPrompt(string prompt, string model = "llama3.2")
    {
        try
        {
            _logger.LogInformation("Sending chat prompt to Ollama");

            var request = new GenerateRequest
            {
                Model = _modelName,
                Prompt = prompt,
                Stream = false,
                KeepAlive = "5m"
            };

            var responseContentBuilder = new StringBuilder();

            await foreach (var responseChunk in _ollamaClient.Generate(request, CancellationToken.None))
            {
                if (responseChunk != null && !string.IsNullOrEmpty(responseChunk.Response))
                {
                    responseContentBuilder.Append(responseChunk.Response);

                    // Log generation progress
                    if (responseChunk.Done)
                    {
                        _logger.LogInformation($"Generation completed at: {responseChunk.CreatedAt}");
                    }
                }
            }

            var response = responseContentBuilder.ToString();
            _logger.LogInformation($"Received response: {response}");

            if (string.IsNullOrEmpty(response))
            {
                throw new Exception("Empty response from Ollama");
            }

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Ollama request failed: {ex.Message}");
            throw new Exception($"Ollama request failed: {ex.Message}");
        }
    }

    private string CreateJsonStructure<T>() where T : class
    {
        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var jsonObject = new JObject();

        foreach (var property in properties)
        {
            jsonObject[property.Name] = CreatePropertyPlaceholder(property.PropertyType);
        }

        return jsonObject.ToString(Formatting.Indented);
    }

    private JToken CreatePropertyPlaceholder(Type type)
    {
        if (type == typeof(string))
            return "string";
        if (type == typeof(int) || type == typeof(long) || type == typeof(float) || type == typeof(double))
            return "number";
        if (type == typeof(bool))
            return "boolean";
        if (type.IsArray || type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
        {
            var elementType = type.IsArray ? type.GetElementType() : type.GetGenericArguments()[0];
            return new JArray(CreatePropertyPlaceholder(elementType));
        }

        if (type.IsClass && type != typeof(string))
        {
            var nestedObject = new JObject();
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                nestedObject[property.Name] = CreatePropertyPlaceholder(property.PropertyType);
            }
            return nestedObject;
        }

        return "dynamic";
    }

    private string CleanJsonResponse(string response)
    {
        response = response.Trim();

        // Remove markdown code block markers if present
        if (response.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
            response = response.Substring(7);
        else if (response.StartsWith("```", StringComparison.OrdinalIgnoreCase))
            response = response.Substring(3);

        if (response.EndsWith("```"))
            response = response[..^3];

        return response.Trim();
    }
}