using System.Text;
using Jarvis.Ai.Common.Settings;
using Jarvis.Ai.Features.StarkArsenal.ModuleAttributes;
using Jarvis.Ai.Interfaces;
using Jarvis.Ai.LLM;
using Jarvis.Ai.Models;
using Newtonsoft.Json;
using TextCopy;

namespace Jarvis.Ai.Features.StarkArsenal.Modules;

[JarvisTacticalModule("Gets a URL from the clipboard, scrapes its content, and saves it to a file in the ISOLATION_AREA.")]
public class ScrapToFileFromClipboardJarvisModule : BaseJarvisModule
{
    private readonly IJarvisConfigManager _jarvisConfigManager;
    private readonly ILlmClient _llmClient;
    private readonly IJarvisLogger _logger;
    private readonly string _apiKey;
    private string _apiUrl = "https://api.firecrawl.dev";
    private static readonly HttpClient _httpClient = new HttpClient();

    public ScrapToFileFromClipboardJarvisModule(
        IJarvisConfigManager jarvisConfigManager, 
        ILlmClient llmClient,
        IJarvisLogger logger)
    {
        _jarvisConfigManager = jarvisConfigManager;
        _llmClient = llmClient;
        _logger = logger;
        _apiKey = jarvisConfigManager.GetValue("FIRECRAWL_API_KEY");
        
        if (string.IsNullOrEmpty(_apiKey))
        {
            _logger.LogError("No API key provided");
            throw new ArgumentException("No API key provided");
        }
    }

    private Dictionary<string, string> PrepareHeaders(string idempotencyKey = null)
    {
        var headers = new Dictionary<string, string>
        {
            { "Authorization", $"Bearer {_apiKey}" }
        };

        if (!string.IsNullOrEmpty(idempotencyKey))
        {
            headers["x-idempotency-key"] = idempotencyKey;
        }

        return headers;
    }

    private async Task<Dictionary<string, object>> ScrapeUrl(string url)
    {
        var headers = PrepareHeaders();
        var scrapeParams = new Dictionary<string, object> { { "url", url } };
        var jsonContent = JsonConvert.SerializeObject(scrapeParams);
        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        var request = new HttpRequestMessage(HttpMethod.Post, $"{_apiUrl}/v0/scrape");
        request.Content = content;

        foreach (var header in headers)
        {
            request.Headers.Add(header.Key, header.Value);
        }

        var response = await _httpClient.SendAsync(request);

        if (response.StatusCode == System.Net.HttpStatusCode.OK)
        {
            var responseString = await response.Content.ReadAsStringAsync();
            var responseData = JsonConvert.DeserializeObject<Dictionary<string, object>>(responseString);

            if (responseData != null && responseData.ContainsKey("success") && (bool)responseData["success"] &&
                responseData.ContainsKey("data"))
            {
                return JsonConvert.DeserializeObject<Dictionary<string, object>>(
                    responseData["data"].ToString());
            }
            
            _logger.LogError($"Failed to scrape URL. Error: {responseData["error"]}");
            throw new Exception($"Failed to scrape URL. Error: {responseData["error"]}");
        }

        throw new HttpRequestException($"Failed to scrape URL. Status code: {response.StatusCode}");
    }

    protected override async Task<Dictionary<string, object>> ExecuteComponentAsync(CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            string scratchPadDir = _jarvisConfigManager.GetValue("ISOLATION_AREA") ?? "./scratchpad";
            Directory.CreateDirectory(scratchPadDir);

            string? url = await ClipboardService.GetTextAsync();
            url = url!.Trim();

            if (!Uri.IsWellFormedUriString(url, UriKind.Absolute))
            {
                return new Dictionary<string, object>
                {
                    { "status", "error" },
                    { "message", "Clipboard content is not a valid URL" },
                };
            }

            cancellationToken.ThrowIfCancellationRequested();

            string fileNamePrompt = $@"
<purpose>
    Generate a suitable file name for the content of this URL: {url}
</purpose>

<instructions>
    <instruction>Create a short, descriptive file name based on the URL.</instruction>
    <instruction>Use lowercase letters, numbers, and underscores only.</instruction>
    <instruction>Use English name.</instruction>
    <instruction>Include the .md extension at the end.</instruction>
</instructions>
";

            CreateFileResponse fileNameResponse = await _llmClient.StructuredOutputPrompt<CreateFileResponse>(
                fileNamePrompt,
                Constants.ModelNameToId[ModelName.FastModel]);
                
            string fileName = fileNameResponse.FileName;

            cancellationToken.ThrowIfCancellationRequested();

            var scrapeResult = await ScrapeUrl(url);
            string content = scrapeResult.ContainsKey("content") ? scrapeResult["content"].ToString() : "";

            cancellationToken.ThrowIfCancellationRequested();

            string filePath = Path.Combine(scratchPadDir, fileName);
            await File.WriteAllTextAsync(filePath, content);

            return new Dictionary<string, object>
            {
                { "status", "success" },
                { "message", $"Content scraped and saved to {filePath}" },
                { "file_name", fileName },
                { "file_path", filePath },
                { "content_length", content.Length }
            };
        }
        catch (OperationCanceledException)
        {
            return new Dictionary<string, object>
            {
                { "status", "cancelled" },
                { "message", "Operation was cancelled" }
            };
        }
        catch (Exception e)
        {
            _logger.LogError($"Failed to scrape URL and save to file: {e.Message}");
            return new Dictionary<string, object>
            {
                { "status", "error" },
                { "message", $"Failed to scrape URL and save to file: {e.Message}" },
            };
        }
    }
}
