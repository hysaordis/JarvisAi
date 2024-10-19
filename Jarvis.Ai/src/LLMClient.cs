using System.Reflection;
using System.Text;
using Jarvis.Ai.src.Interfaces;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Jarvis.Ai;

public class LlmClient
{
    private readonly string _apiKey;
    private readonly HttpClient _httpClient = new();
    
    public LlmClient(IJarvisConfigManager configManager)
    {
        var key = configManager.GetValue("OPENAI_API_KEY");
        if (string.IsNullOrEmpty(key))
        {
            throw new Exception("OPENAI_API_KEY environment variable not set.");
        }
        _apiKey = key;
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
            model = model,
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
        if (type.IsArray || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)))
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
            model = model,
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