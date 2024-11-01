using System.Reflection;
using System.Text;
using Jarvis.Ai.Common.Settings;
using Jarvis.Ai.Interfaces;
using Jarvis.Ai.Models;
using Jarvis.Ai.Persistence;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace Jarvis.Ai.LLM;

public class OpenAiLlmClient : ILlmClient
{
    private readonly string _apiKey;
    private readonly HttpClient _httpClient = new();
    private readonly IStarkArsenal _starkArsenal;
    private readonly IJarvisLogger _logger;
    private readonly IConversationStore _conversationStore;

    public OpenAiLlmClient(IJarvisConfigManager configManager, IStarkArsenal starkArsenal, IJarvisLogger logger, IConversationStore conversationStore)
    {
        var key = configManager.GetValue("OPENAI_API_KEY");
        if (string.IsNullOrEmpty(key))
        {
            throw new Exception("OPENAI_API_KEY environment variable not set.");
        }
        _apiKey = key;
        _starkArsenal = starkArsenal;
        _logger = logger;
        _conversationStore = conversationStore;
    }

    public async Task<Message> SendCommandToLlmAsync(List<Message> messages, CancellationToken cancellationToken)
    {
        try
        {
            var ollamaTools = _starkArsenal.GetToolsForOllama();
            var openAiTools = ToolMapper.ConvertToOpenAiTools(ollamaTools);

            var conversationHistory = await _conversationStore.GetAllMessagesAsync();

            var messagesPrepared = conversationHistory.Select(msg =>
            {
                var msgDict = new Dictionary<string, object>
                {
                    { "role", msg.Role }
                };

                if (msg.Content != null)
                    msgDict.Add("content", msg.Content);

                if (msg.ToolCalls != null)
                    msgDict.Add("tool_calls", msg.ToolCalls.Select(tc => new
                    {
                        id = tc.Id,
                        type = tc.Type,
                        function = new
                        {
                            name = tc.Function.Name,
                            arguments = JsonConvert.SerializeObject(tc.Function.Arguments)
                        }
                    }).ToList());

                if (msg.ToolCallId != null)
                    msgDict.Add("tool_call_id", msg.ToolCallId);

                return msgDict;
            }).ToList();

            var requestBody = new
            {
                model = Constants.ModelNameToId[Enum.Parse<ModelName>(ModelName.BaseModel.ToString())],
                messages = messagesPrepared,
                tools = openAiTools,
                tool_choice = "auto",
                stream = false
            };

            _logger.LogDebug($"Request Body: {JsonConvert.SerializeObject(requestBody, Formatting.Indented)}");

            var settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                ContractResolver = new DefaultContractResolver
                {
                    NamingStrategy = new CamelCaseNamingStrategy
                    {
                        OverrideSpecifiedNames = false
                    }
                }
            };

            var jsonContent = JsonConvert.SerializeObject(requestBody, settings);
            var requestContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");

            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions")
            {
                Content = requestContent
            };

            using var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var responseJson = JObject.Parse(responseContent);

                // Parse the response
                var choices = responseJson["choices"] as JArray;
                if (choices == null || choices.Count == 0)
                {
                    throw new Exception("No choices returned from OpenAI API.");
                }

                var firstChoice = choices[0];
                var messageJson = firstChoice["message"];

                var assistantMessage = new Message
                {
                    Role = messageJson["role"].ToString(),
                    Content = messageJson["content"]?.ToString()
                };

                // Check for function calls (tool_calls)
                if (messageJson["tool_calls"] != null)
                {
                    var toolCallsJson = messageJson["tool_calls"] as JArray;
                    var functionCalls = new List<FunctionCall>();
                    string id = string.Empty;

                    foreach (var toolCall in toolCallsJson)
                    {
                        var function = toolCall["function"];
                        id = toolCall["id"].ToString();
                        var functionCall = new FunctionCall
                        {
                            Id = id,
                            Type = toolCall["type"].ToString(),
                            Function = new FunctionDetail
                            {
                                Name = function["name"].ToString(),
                                Arguments = function["arguments"] != null
                                    ? JsonConvert.DeserializeObject<Dictionary<string, object>>(function["arguments"].ToString())
                                    : new Dictionary<string, object>()
                            }
                        };

                        functionCalls.Add(functionCall);
                    }

                    assistantMessage.ToolCalls = functionCalls;
                }

                // Add the assistant's message to the conversation history
                await _conversationStore.SaveMessageAsync(assistantMessage);

                // Return the assistant's message
                return assistantMessage;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new Exception($"OpenAI API request failed: {errorContent}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error in SendCommandToLlmAsync: {ex.Message}");
            throw;
        }
    }

    public async Task<T> StructuredOutputPrompt<T>(string prompt, string model = "gpt-4") where T : class
    {
        var jsonStructure = CreateJsonStructure<T>();
        var jsonPrompt = $@"
{prompt}

<convert-instructions>
    <instruction>Provide your response in the following JSON format.</instruction>
</convert-instructions>
<json-format>
{jsonStructure}
</json-format>
";
        var requestBody = new
        {
            model,
            messages = new[]
            {
                new { role = "system", content = "You are a helpful assistant that always responds in the specified JSON format." },
                new { role = "user", content = jsonPrompt }
            },
            response_format = new { type = "json_object" }
        };

        var jsonContent = JsonConvert.SerializeObject(requestBody);
        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
        var response = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);

        if (response.IsSuccessStatusCode)
        {
            var responseString = await response.Content.ReadAsStringAsync();
            var responseJson = JObject.Parse(responseString);
            var messageContent = responseJson["choices"]![0]!["message"]!["content"]!.ToString();
            try
            {
                var settings = new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore
                };
                T result = JsonConvert.DeserializeObject<T>(messageContent, settings)!;
                return result;
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to parse the assistant's response into the expected format: " + ex.Message);
            }
        }

        var errorContent = await response.Content.ReadAsStringAsync();
        throw new Exception($"OpenAI API request failed: {errorContent}");
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

    public async Task<string> ChatPrompt(string prompt, string model = "gpt-4")
    {
        var requestBody = new
        {
            model,
            messages = new[]
            {
                new { role = "user", content = prompt }
            }
        };

        var jsonContent = JsonConvert.SerializeObject(requestBody);
        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");

        var response = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);

        if (response.IsSuccessStatusCode)
        {
            var responseString = await response.Content.ReadAsStringAsync();
            var responseJson = JObject.Parse(responseString);

            var messageContent = responseJson["choices"][0]["message"]["content"].ToString();
            return messageContent;
        }
        else
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new Exception($"OpenAI API request failed: {errorContent}");
        }
    }
}
