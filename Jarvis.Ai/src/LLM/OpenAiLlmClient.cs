using System.Reflection;
using System.Text;
using Jarvis.Ai.Common.Settings;
using Jarvis.Ai.Interfaces;
using Jarvis.Ai.Models;
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

    public OpenAiLlmClient(IJarvisConfigManager configManager, IStarkArsenal starkArsenal, IJarvisLogger logger)
    {
        var key = configManager.GetValue("OPENAI_API_KEY");
        if (string.IsNullOrEmpty(key))
        {
            throw new Exception("OPENAI_API_KEY environment variable not set.");
        }
        _apiKey = key;
        _starkArsenal = starkArsenal;
        _logger = logger;
    }

    public async Task<ResponseMessage> SendCommandToLlmAsync(List<ResponseMessage> messages, CancellationToken cancellationToken)
    {
        try
        {
            var ollamaTools = _starkArsenal.GetToolsForOllama();
            var openAiTools = ToolMapper.ConvertToOpenAiTools(ollamaTools);

            var validMessages = new List<object>();

            var messageGroups = new List<List<ResponseMessage>>();
            var currentGroup = new List<ResponseMessage>();

            foreach (var msg in messages)
            {
                if (msg.Role.ToLower() == "user" || msg.Role.ToLower() == "system")
                {
                    if (currentGroup.Any()) messageGroups.Add(currentGroup.ToList());
                    currentGroup = new List<ResponseMessage> { msg };
                }
                else
                {
                    currentGroup.Add(msg);
                }
            }
            if (currentGroup.Any()) messageGroups.Add(currentGroup);

            foreach (var group in messageGroups)
            {
                var pendingToolCalls = new Dictionary<string, bool>();

                foreach (var msg in group)
                {
                    switch (msg.Role.ToLower())
                    {
                        case "system":
                        case "user":
                            validMessages.Add(new { role = msg.Role.ToLower(), content = msg.Content });
                            break;

                        case "assistant":
                            if (msg.ToolCalls?.Any() == true)
                            {
                                var toolCallsArray = msg.ToolCalls.Select((tool, index) =>
                                {
                                    var toolCallId = $"call_{validMessages.Count}_{index}";
                                    pendingToolCalls[toolCallId] = false;
                                    return new
                                    {
                                        id = toolCallId,
                                        type = "function",
                                        function = new
                                        {
                                            name = tool.Name,
                                            arguments = JsonConvert.SerializeObject(tool.Parameters)
                                        }
                                    };
                                }).ToList();

                                validMessages.Add(new
                                {
                                    role = "assistant",
                                    content = msg.Content,
                                    tool_calls = toolCallsArray
                                });
                            }
                            else
                            {
                                validMessages.Add(new { role = "assistant", content = msg.Content });
                            }
                            break;

                        case "tool":
                            var pendingCall = pendingToolCalls.FirstOrDefault(x => !x.Value);
                            if (!string.IsNullOrEmpty(pendingCall.Key))
                            {
                                validMessages.Add(new
                                {
                                    role = "tool",
                                    content = msg.Content,
                                    tool_call_id = pendingCall.Key
                                });
                                pendingToolCalls[pendingCall.Key] = true;
                            }
                            break;
                    }
                }

                if (pendingToolCalls.Any(x => !x.Value))
                {
                    if (group != messageGroups.Last())
                    {
                        throw new Exception($"Unhandled tool calls in conversation: {string.Join(", ", pendingToolCalls.Where(x => !x.Value).Select(x => x.Key))}");
                    }
                }
            }

            var requestBody = new
            {
                model = Constants.ModelNameToId[Enum.Parse<ModelName>(ModelName.BaseModel.ToString())],
                messages = validMessages,
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
                    NamingStrategy = new CamelCaseNamingStrategy()
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
                var responseString = await response.Content.ReadAsStringAsync(cancellationToken);
                var responseJson = JObject.Parse(responseString);

                var message = responseJson["choices"]?[0]?["message"];
                if (message == null)
                {
                    throw new Exception("Invalid response format from OpenAI API");
                }

                var role = message["role"]?.ToString() ?? "assistant";
                var messageContent = message["content"]?.ToString();
                var toolCalls = message["tool_calls"];

                if (toolCalls != null && toolCalls.Any())
                {
                    var parsedToolCalls = new List<OllamaTools>();
                    foreach (var toolCall in toolCalls)
                    {
                        var function = toolCall["function"];
                        if (function != null)
                        {
                            try
                            {
                                var argumentsStr = function["arguments"]?.ToString() ?? "{}";
                                var parameters = JsonConvert.DeserializeObject<Dictionary<string, object>>(argumentsStr);

                                parsedToolCalls.Add(new OllamaTools
                                {
                                    Type = "function",
                                    Name = function["name"]?.ToString(),
                                    Parameters = parameters
                                });
                            }
                            catch (JsonException ex)
                            {
                                _logger.LogError($"Error parsing tool call arguments: {ex.Message}");
                                continue;
                            }
                        }
                    }

                    return new ResponseMessage
                    {
                        Role = role,
                        Content = messageContent ?? string.Empty,
                        ToolCalls = parsedToolCalls
                    };
                }

                return new ResponseMessage
                {
                    Role = role,
                    Content = messageContent ?? throw new Exception("Empty content in OpenAI response"),
                    ToolCalls = null
                };
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
