using System.Diagnostics;
using Jarvis.Ai.Common.Settings;
using Jarvis.Ai.Features.StarkArsenal.ModuleAttributes;
using Jarvis.Ai.Interfaces;
using Jarvis.Ai.LLM;
using Jarvis.Ai.Models;
using Newtonsoft.Json;

namespace Jarvis.Ai.Features.StarkArsenal.Modules;

[JarvisTacticalModule(@"Opens a web browser based on user's request. Use this module when:
- User wants to read or search something on specific websites (e.g., Stack Overflow, GitHub, Google)
- User asks to open a specific URL or website
- User wants to browse the internet for technical information
- User mentions 'open website', 'open browser', 'search online', 'read about', 'look up'
- User wants to check professional social media (e.g., LinkedIn, Twitter)
- User needs to access web-based development tools or platforms
Examples:
- 'I want to read about async programming on Stack Overflow'
- 'Open GitHub'
- 'Search for C# tutorials'
- 'Can you open Google for me?'")]
public class OpenBrowserJarvisModule : BaseJarvisModule
{
    [TacticalComponent("The user's prompt to determine which URL to open.", "string", true)]
    public string Prompt { get; set; }

    private readonly StarkProtocols _starkProtocols;
    private readonly ILlmClient _llmClient;
    private readonly IJarvisLogger _jarvisLogger;

    public OpenBrowserJarvisModule(StarkProtocols starkProtocols, ILlmClient llmClient, IJarvisLogger jarvisLogger)
    {
        _starkProtocols = starkProtocols;
        _llmClient = llmClient;
        _jarvisLogger = jarvisLogger;
    }

    protected override async Task<Dictionary<string, object>> ExecuteComponentAsync(CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var browserUrls = _starkProtocols.GetBrowserUrls();
            string browserUrlsStr = string.Join(", ", browserUrls);

            string promptStructure = $@"
<purpose>
    Determine if the user's prompt is to open a specific URL or to perform a search.
</purpose>

<instructions>
    <instruction>If the user's prompt matches one of the browser URLs, return the URL.</instruction>
    <instruction>If the user's prompt is a search query, return an empty string.</instruction>
</instructions>

<browser-urls>
    {browserUrlsStr}
</browser-urls>

<user-prompt>
    {Prompt}
</user-prompt>
";

            _jarvisLogger.LogInformation($"📖 open_browser() Prompt: {promptStructure}");

            cancellationToken.ThrowIfCancellationRequested();

            WebUrl response =
                await _llmClient.StructuredOutputPrompt<WebUrl>(promptStructure,
                    Constants.ModelNameToId[ModelName.FastModel]);

            _jarvisLogger.LogInformation($"📖 open_browser() Response: {JsonConvert.SerializeObject(response)}");

            if (!string.IsNullOrEmpty(response.Url))
            {
                return await OpenUrlAsync(response.Url, cancellationToken);
            }
            else
            {
                return await HandleSearchQueryAsync(cancellationToken);
            }
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
            return new Dictionary<string, object>
            {
                { "status", "error" },
                { "message", $"Failed to process browser request: {e.Message}" }
            };
        }
    }

    private async Task<Dictionary<string, object>> OpenUrlAsync(string url, CancellationToken cancellationToken)
    {
        _jarvisLogger.LogInformation($"📖 open_browser() Opening URL: {url}");
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
            return new Dictionary<string, object>
            {
                { "status", "Browser opened" },
                { "url", url },
            };
        }
        catch (Exception e)
        {
            _jarvisLogger.LogError($"Failed to open browser: {e.Message}");
            return new Dictionary<string, object>
            {
                { "status", "Error" },
                { "message", $"Failed to open browser: {e.Message}" },
            };
        }
    }

    private async Task<Dictionary<string, object>> HandleSearchQueryAsync(CancellationToken cancellationToken)
    {
        string searchQuery = Uri.EscapeDataString(Prompt);
        string searchUrl = $"https://www.google.com/search?q={searchQuery}";

        if (Prompt.ToLower().Contains("youtube"))
        {
            searchUrl = $"https://www.youtube.com/results?search_query={searchQuery}";
        }

        _jarvisLogger.LogInformation($"📖 open_browser() Performing search with URL: {searchUrl}");
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            Process.Start(new ProcessStartInfo
            {
                FileName = searchUrl,
                UseShellExecute = true
            });
            return new Dictionary<string, object>
            {
                { "status", "Browser opened with search query" },
                { "url", searchUrl },
            };
        }
        catch (Exception e)
        {
            _jarvisLogger.LogError($"Failed to open browser with search query: {e.Message}");
            return new Dictionary<string, object>
            {
                { "status", "Error" },
                { "message", $"Failed to open browser with search query: {e.Message}" },
            };
        }
    }
}
